function createHandlers({ players, npcs, stats, config, broadcast }) {
  function handleMove(ws, msg) {
    if (typeof msg.id !== 'string') return;
    if (typeof msg.x !== 'number' || typeof msg.y !== 'number') return;

    const now = Date.now() / 1000;
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
