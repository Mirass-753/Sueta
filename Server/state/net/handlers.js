function createHandlers({ players, npcs, stats, config, attacks, broadcast }) {
  const debugAi = String(process.env.DEBUG_AI || '').toLowerCase() === 'true'
    || process.env.DEBUG_AI === '1';
  const lastMoveLogAt = new Map();

  function worldToCell(x, y) {
    const fx = x / config.GRID_SIZE - config.CELL_CENTER_OFFSET_X;
    const fy = y / config.GRID_SIZE - config.CELL_CENTER_OFFSET_Y;
    return { x: Math.round(fx), y: Math.round(fy) };
  }

  function isFiniteNumber(value) {
    return Number.isFinite(value);
  }

  function normalizeVector(x, y) {
    const len = Math.hypot(x, y) || 1;
    return { x: x / len, y: y / len };
  }

  function getNpcAttackRangeStart() {
    if (typeof config.NPC_ATTACK_RANGE_START === 'number') {
      return config.NPC_ATTACK_RANGE_START;
    }
    return config.NPC_ATTACK_RANGE;
  }

  function getNpcAttackRangeEpsilon() {
    if (typeof config.NPC_ATTACK_RANGE_EPSILON === 'number') {
      return config.NPC_ATTACK_RANGE_EPSILON;
    }
    return typeof config.GRID_SIZE === 'number' ? config.GRID_SIZE * 0.05 : 0.05;
  }

  function handleMove(ws, msg) {
    if (typeof msg.id !== 'string') return;
    if (typeof msg.x !== 'number' || typeof msg.y !== 'number') return;

    const now = Date.now() / 1000;
    if (debugAi) {
      const nowMs = Date.now();
      const lastLog = lastMoveLogAt.get(msg.id) || 0;
      if (nowMs - lastLog >= 1000) {
        console.log('[WS] move', msg.id, msg.x, msg.y);
        lastMoveLogAt.set(msg.id, nowMs);
      }
    }
    const prev = players.getPlayer(msg.id);

    let x = msg.x;
    let y = msg.y;
    let vx = 0;
    let vy = 0;
    let dt = 1 / 60;

    if (prev) {
      dt = Math.max(now - prev.t, 1 / 60);
      const dx = x - prev.x;
      const dy = y - prev.y;
      const dist = Math.sqrt(dx * dx + dy * dy);
      const speed = dist / dt;

      if (speed > config.MAX_SPEED) {
        const maxDist = config.MAX_SPEED * dt;

        if (dist > 0) {
          const scale = maxDist / dist;
          x = prev.x + dx * scale;
          y = prev.y + dy * scale;
        } else {
          x = prev.x;
          y = prev.y;
        }
      }
    }

    if (prev) {
      vx = (x - prev.x) / dt;
      vy = (y - prev.y) / dt;
    }

    ws.playerId = ws.playerId || msg.id;

    stats.getHp(msg.id);
    stats.getEnergy(msg.id);

    players.setPlayer(msg.id, {
      x,
      y,
      vx,
      vy,
      dirX: typeof msg.dirX === 'number' ? msg.dirX : 0,
      dirY: typeof msg.dirY === 'number' ? msg.dirY : 0,
      moving: !!msg.moving,
      aimAngle: typeof msg.aimAngle === 'number' ? msg.aimAngle : 0,
      inCombat: !!msg.inCombat,
      t: now,
    });

    const out = {
      type: 'move',
      id: msg.id,
      x,
      y,
      dirX: typeof msg.dirX === 'number' ? msg.dirX : 0,
      dirY: typeof msg.dirY === 'number' ? msg.dirY : 0,
      moving: !!msg.moving,
      aimAngle: typeof msg.aimAngle === 'number' ? msg.aimAngle : 0,
      inCombat: !!msg.inCombat,
    };

    broadcast(out, ws);
  }

  function handleDamageRequest(ws, msg) {
    const sourceId = typeof msg.sourceId === 'string' ? msg.sourceId : null;
    const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
    const amount = typeof msg.amount === 'number' ? msg.amount : 0;

    if (!targetId || amount <= 0) return;

    const oldHp = stats.getHp(targetId);
    const newHp = stats.setHp(targetId, oldHp - amount);
    const appliedDamage = Math.max(0, oldHp - newHp);

    const evt = {
      type: 'damage',
      sourceId,
      targetId,
      amount,
      hp: newHp,
    };

    console.log('[WS] damage', evt);

    if (appliedDamage > 0) {
      const targetState = players.getPlayer(targetId) || {};
      const popupX = typeof msg.x === 'number'
        ? msg.x
        : typeof targetState.x === 'number'
          ? targetState.x
          : 0;
      const popupY = typeof msg.y === 'number'
        ? msg.y
        : typeof targetState.y === 'number'
          ? targetState.y
          : 0;
      const popupZ = typeof msg.z === 'number'
        ? msg.z
        : 0;
      const popupZone = typeof msg.zone === 'string' ? msg.zone : '';

      const popupMsg = {
        type: 'damage_popup',
        amount: Math.round(appliedDamage),
        x: popupX,
        y: popupY,
        z: popupZ,
      };
      broadcast(popupMsg);

      const hitFxMsg = {
        type: 'hit_fx',
        fx: 'claws',
        targetId,
        zone: popupZone,
        x: popupX,
        y: popupY,
        z: popupZ,
      };
      broadcast(hitFxMsg);
    }

    broadcast(evt);

    if (npcs.hasNpc(targetId)) {
      const npc = npcs.getNpc(targetId);
      const updated = {
        x: npc.x,
        y: npc.y,
        hp: newHp,
      };
      npcs.setNpc(targetId, updated);

      if (newHp <= 0) {
        npcs.deleteNpc(targetId);
        stats.deleteHp(targetId);
        broadcast({ type: 'npc_despawn', npcId: targetId });
      }
    }
  }

  function handlePlayerAttackRequest(ws, msg) {
    const sourceId = ws.playerId || (typeof msg.sourceId === 'string' ? msg.sourceId : null);
    if (!sourceId) return;
    if (!ws.playerId) ws.playerId = sourceId;

    const dirX = typeof msg.dirX === 'number' ? msg.dirX : 0;
    const dirY = typeof msg.dirY === 'number' ? msg.dirY : 0;
    const weapon = typeof msg.weapon === 'string' ? msg.weapon : 'claws';

    const attackId = attacks.createAttack({
      sourceId,
      dirX,
      dirY,
      weapon,
      windowSeconds: config.PLAYER_ATTACK_WINDOW_SECONDS,
    });

    broadcast({
      type: 'attack_start',
      attackId,
      sourceId,
      targetId: null,
      dirX,
      dirY,
      weapon,
    });
  }

  function handleAttackHitReport(ws, msg) {
    const attackId = typeof msg.attackId === 'string' ? msg.attackId : null;
    const sourceId = typeof msg.sourceId === 'string' ? msg.sourceId : null;
    const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
    const hitPart = typeof msg.hitPart === 'string' ? msg.hitPart : '';

    if (!attackId || !sourceId || !targetId) return;

    const attack = attacks.getAttack(attackId);
    if (!attack || attack.sourceId !== sourceId) return;
    if (attack.targetId && attack.targetId !== targetId) return;

    const now = Date.now() / 1000;
    if (!attacks.isAttackActive(attack, now)) {
      attacks.removeAttack(attackId);
      return;
    }

    const sourceNpc = npcs.hasNpc(sourceId) ? npcs.getNpc(sourceId) : null;
    const sourcePlayer = players.getPlayer(sourceId);
    const targetNpc = npcs.hasNpc(targetId) ? npcs.getNpc(targetId) : null;
    const targetPlayer = players.getPlayer(targetId);

    const source = sourceNpc || sourcePlayer;
    const target = targetNpc || targetPlayer;

    if (!source || !target) return;

    const dx = target.x - source.x;
    const dy = target.y - source.y;
    const distance = Math.hypot(dx, dy);

    const range = sourceNpc ? config.NPC_ATTACK_RANGE : config.PLAYER_ATTACK_RANGE;
    if (distance > range) return;

    const dirX = typeof attack.dirX === 'number' ? attack.dirX : 0;
    const dirY = typeof attack.dirY === 'number' ? attack.dirY : 0;
    const dirLen = Math.hypot(dirX, dirY);
    if (dirLen === 0) return;

    const toTarget = normalizeVector(dx, dy);
    const facingDot = sourceNpc ? config.NPC_ATTACK_FACING_DOT : config.PLAYER_ATTACK_FACING_DOT;
    const dot = (dirX * toTarget.x + dirY * toTarget.y) / dirLen;
    if (dot < facingDot) return;

    const baseDamage = sourceNpc ? config.NPC_ATTACK_DAMAGE : config.PLAYER_ATTACK_DAMAGE;
    const partMultiplier = config.HIT_PART_MULTIPLIERS[hitPart] || 1;
    const rawDamage = baseDamage * partMultiplier;

    const oldHp = stats.getHp(targetId);
    const newHp = stats.setHp(targetId, oldHp - rawDamage);
    const appliedDamage = Math.max(0, oldHp - newHp);

    const evt = {
      type: 'damage',
      sourceId,
      targetId,
      amount: appliedDamage,
      hp: newHp,
      hitPart,
    };

    console.log('[WS] attack damage', evt);

    if (appliedDamage > 0) {
      const popupX = typeof msg.x === 'number' ? msg.x : target.x;
      const popupY = typeof msg.y === 'number' ? msg.y : target.y;
      const popupZ = typeof msg.z === 'number' ? msg.z : 0;

      broadcast({
        type: 'damage_popup',
        amount: Math.round(appliedDamage),
        x: popupX,
        y: popupY,
        z: popupZ,
      });

      broadcast({
        type: 'hit_fx',
        fx: 'claws',
        targetId,
        zone: hitPart,
        x: popupX,
        y: popupY,
        z: popupZ,
      });
    }

    broadcast(evt);

    if (targetNpc) {
      const updated = {
        x: targetNpc.x,
        y: targetNpc.y,
        hp: newHp,
      };
      npcs.setNpc(targetId, updated);

      if (newHp <= 0) {
        npcs.deleteNpc(targetId);
        stats.deleteHp(targetId);
        broadcast({ type: 'npc_despawn', npcId: targetId });
      }
    }

    attacks.removeAttack(attackId);
  }

  function handleNpcAttackRequest(ws, msg) {
    const npcId = typeof msg.npcId === 'string' ? msg.npcId : null;
    const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;

    if (!npcId || !targetId) return;
    if (!ws.playerId || ws.playerId !== targetId) return;

    const npc = npcs.getNpc(npcId);
    const target = players.getPlayer(targetId);
    const meta = npcs.getNpcMeta ? npcs.getNpcMeta(npcId) : null;

    if (!npc || !target || !meta || meta.state !== 'Attack') return;
    if (meta && meta.targetPlayerId && meta.targetPlayerId !== targetId) return;

    const now = Date.now() / 1000;
    const windowUntil = meta && typeof meta.attackWindowUntil === 'number'
      ? meta.attackWindowUntil
      : -Infinity;
    if (now > windowUntil) return;

    if (![npc.x, npc.y, target.x, target.y].every(isFiniteNumber)) return;

    const dx = target.x - npc.x;
    const dy = target.y - npc.y;
    const distance = Math.hypot(dx, dy);

    if (!isFiniteNumber(distance)) return;
    const attackRange = getNpcAttackRangeStart();
    const attackRangeEpsilon = getNpcAttackRangeEpsilon();
    if (distance > attackRange + attackRangeEpsilon) return;

    const npcCell = worldToCell(npc.x, npc.y);
    const targetCell = worldToCell(target.x, target.y);
    const cellDx = targetCell.x - npcCell.x;
    const cellDy = targetCell.y - npcCell.y;
    const adjacent = Math.abs(cellDx) <= 1 && Math.abs(cellDy) <= 1 && (cellDx !== 0 || cellDy !== 0);
    if (!adjacent) return;

    if (meta && (meta.dirX !== 0 || meta.dirY !== 0)) {
      const dir = normalizeVector(meta.dirX, meta.dirY);
      const toPlayer = normalizeVector(dx, dy);
      const facingDot = typeof config.NPC_ATTACK_FACING_DOT === 'number'
        ? config.NPC_ATTACK_FACING_DOT
        : 0.5;
      if (dir.x * toPlayer.x + dir.y * toPlayer.y < facingDot) return;
    } else {
      return;
    }

    const oldHp = stats.getHp(targetId);
    const newHp = stats.setHp(targetId, oldHp - config.NPC_ATTACK_DAMAGE);
    const appliedDamage = Math.max(0, oldHp - newHp);

    const evt = {
      type: 'damage',
      sourceId: npcId,
      targetId,
      amount: appliedDamage,
      hp: newHp,
    };

    console.log('[WS] npc damage', evt);

    if (appliedDamage > 0) {
      const popupMsg = {
        type: 'damage_popup',
        amount: Math.round(appliedDamage),
        x: target.x,
        y: target.y,
        z: 0,
      };
      broadcast(popupMsg);

      const hitFxMsg = {
        type: 'hit_fx',
        fx: 'claws',
        targetId,
        zone: '',
        x: target.x,
        y: target.y,
        z: 0,
      };
      broadcast(hitFxMsg);
    }

    broadcast(evt);
  }

  function handleEnergyRequest(ws, msg) {
    const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
    const amount = typeof msg.amount === 'number' ? msg.amount : 0;

    if (!targetId || amount <= 0) return;

    const oldEnergy = stats.getEnergy(targetId);
    const newEnergy = stats.setEnergy(targetId, oldEnergy - amount);

    const evt = {
      type: 'energy_update',
      targetId,
      energy: newEnergy,
      maxEnergy: config.DEFAULT_ENERGY,
    };

    console.log('[WS] energy_update', evt);

    broadcast(evt);
  }

  function handleItemDrop(ws, msg) {
    if (!msg || typeof msg.pickupId !== 'string' || typeof msg.itemName !== 'string') return;

    const evt = {
      type: 'item_drop',
      pickupId: msg.pickupId,
      itemName: msg.itemName,
      x: typeof msg.x === 'number' ? msg.x : 0,
      y: typeof msg.y === 'number' ? msg.y : 0,
    };

    broadcast(evt, ws);
  }

  function handleItemPickup(ws, msg) {
    if (!msg || typeof msg.pickupId !== 'string') return;

    const evt = {
      type: 'item_pickup',
      pickupId: msg.pickupId,
    };

    if (typeof msg.itemName === 'string') evt.itemName = msg.itemName;
    if (typeof msg.x === 'number') evt.x = msg.x;
    if (typeof msg.y === 'number') evt.y = msg.y;

    broadcast(evt, ws);
  }

  function handleDisconnect(ws) {
    const id = ws.playerId;
    if (!id) return;

    players.removePlayer(id);
    stats.removeEnergyMeta(id);

    const msg = {
      type: 'disconnect',
      id,
    };

    broadcast(msg);
  }

  function onMessage(ws, data) {
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch (err) {
      console.warn('[WS] failed to parse message', err);
      return;
    }

    if (!msg || typeof msg !== 'object') return;
    const type = typeof msg.type === 'string' ? msg.type : 'move';

    switch (type) {
      case 'move':
        handleMove(ws, msg);
        break;
      case 'damage_request':
        handleDamageRequest(ws, msg);
        break;
      case 'player_attack_request':
        handlePlayerAttackRequest(ws, msg);
        break;
      case 'attack_hit_report':
        handleAttackHitReport(ws, msg);
        break;
      case 'npc_attack_request':
        handleNpcAttackRequest(ws, msg);
        break;
      case 'energy_request':
        handleEnergyRequest(ws, msg);
        break;
      case 'item_drop':
        handleItemDrop(ws, msg);
        break;
      case 'item_pickup':
        handleItemPickup(ws, msg);
        break;
      default:
        break;
    }
  }

  return {
    onMessage,
    onDisconnect: handleDisconnect,
  };
}

module.exports = {
  createHandlers,
};
