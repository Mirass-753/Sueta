const fs = require('fs');
const path = require('path');

const DIRECTIONS = [
  { x: 1, y: 0 },
  { x: -1, y: 0 },
  { x: 0, y: 1 },
  { x: 0, y: -1 },
  { x: 1, y: 1 },
  { x: 1, y: -1 },
  { x: -1, y: 1 },
  { x: -1, y: -1 },
];

function startNpcAiLoop({ npcs, players, stats, config, broadcast }) {
  if (!npcs || !players || !stats || !config || !broadcast) {
    return;
  }

  const blockedCells = loadBlockedCells();

  setInterval(() => {
    if (npcs.size() === 0) return;

    const now = Date.now() / 1000;
    const occupancy = buildOccupancy(players, npcs, config);

    for (const [npcId, npc] of npcs.entries()) {
      const hp = stats.getHp(npcId);

      if (hp <= 0) {
        npcs.deleteNpc(npcId);
        stats.deleteHp(npcId);
        broadcast({ type: 'npc_despawn', npcId });
        continue;
      }

      if (npc.hp !== hp) {
        npc.hp = hp;
      }

      const meta = npcs.ensureNpcMeta(npcId);
      ensureMetaDefaults(meta, now);
      const currentCell = worldToCell(npc.x, npc.y, config);
      const npcMaxHp = config.DEFAULT_HP;
      const healthPercent = npcMaxHp > 0 ? hp / npcMaxHp : 1;

      const { player, distanceToPlayer } = selectTarget({
        players,
        stats,
        config,
        meta,
        npcId,
        npcX: npc.x,
        npcY: npc.y,
      });

      if (player && distanceToPlayer <= config.NPC_LOSE_RANGE) {
        meta.lastKnownPlayerCell = worldToCell(player.x, player.y, config);
        meta.lastSeenTime = now;
      }

      updateAiState({
        meta,
        npc,
        player,
        distanceToPlayer,
        healthPercent,
        now,
        config,
      });

      const nextCell = decideAction({
        meta,
        npcId,
        npc,
        currentCell,
        player,
        distanceToPlayer,
        healthPercent,
        now,
        config,
        occupancy,
        blockedCells,
        stats,
        broadcast,
      });

      if (nextCell) {
        const dirX = nextCell.x - currentCell.x;
        const dirY = nextCell.y - currentCell.y;
        const norm = Math.hypot(dirX, dirY) || 1;
        meta.dirX = dirX / norm;
        meta.dirY = dirY / norm;
        meta.moving = true;
        meta.lastMoveTime = now;

        const worldPos = cellToWorld(nextCell, config);
        npc.x = worldPos.x;
        npc.y = worldPos.y;
        npcs.setNpc(npcId, npc);
      } else {
        meta.moving = false;
        meta.dirX = 0;
        meta.dirY = 0;
      }
    }
  }, config.NPC_AI_TICK_MS);
}

function loadBlockedCells() {
  const filePath = path.join(__dirname, '..', 'config', 'blockedCells.json');
  if (!fs.existsSync(filePath)) {
    return new Set();
  }

  try {
    const raw = fs.readFileSync(filePath, 'utf8');
    const data = JSON.parse(raw);
    const blocked = new Set();

    if (Array.isArray(data)) {
      data.forEach((entry) => {
        if (!entry) return;
        if (Array.isArray(entry) && entry.length >= 2) {
          blocked.add(`${entry[0]},${entry[1]}`);
        } else if (typeof entry.x === 'number' && typeof entry.y === 'number') {
          blocked.add(`${entry.x},${entry.y}`);
        }
      });
    }

    return blocked;
  } catch (err) {
    console.warn('[NPC AI] Failed to read blockedCells.json:', err.message);
    return new Set();
  }
}

function ensureMetaDefaults(meta, now) {
  if (typeof meta.lastAttackTime !== 'number') {
    meta.lastAttackTime = -Infinity;
  }
  if (typeof meta.lastMoveTime !== 'number') {
    meta.lastMoveTime = -Infinity;
  }
  if (typeof meta.lastSeenTime !== 'number') {
    meta.lastSeenTime = 0;
  }
  if (typeof meta.patrolIndex !== 'number') {
    meta.patrolIndex = 0;
  }
  if (typeof meta.patrolWaitUntil !== 'number') {
    meta.patrolWaitUntil = 0;
  }
  if (typeof meta.state !== 'string') {
    meta.state = 'Idle';
  }
  if (typeof meta.tStateChange !== 'number') {
    meta.tStateChange = now;
  }
  if (typeof meta.dirX !== 'number') {
    meta.dirX = 0;
  }
  if (typeof meta.dirY !== 'number') {
    meta.dirY = 0;
  }
  if (typeof meta.moving !== 'boolean') {
    meta.moving = false;
  }
}

function buildOccupancy(players, npcs, config) {
  const occupied = new Set();

  for (const [, player] of players.entries()) {
    const cell = worldToCell(player.x, player.y, config);
    occupied.add(`${cell.x},${cell.y}`);
  }

  for (const [, npc] of npcs.entries()) {
    const cell = worldToCell(npc.x, npc.y, config);
    occupied.add(`${cell.x},${cell.y}`);
  }

  return occupied;
}

function selectTarget({ players, stats, config, meta, npcId, npcX, npcY }) {
  const currentTargetId = meta.targetPlayerId;
  let bestPlayer = null;
  let bestDistance = Infinity;

  if (currentTargetId) {
    const candidate = players.getPlayer(currentTargetId);
    if (candidate && stats.getHp(currentTargetId) > 0) {
      const dist = distanceBetween(npcX, npcY, candidate.x, candidate.y);
      if (dist <= config.NPC_LOSE_RANGE) {
        bestPlayer = { id: currentTargetId, ...candidate };
        bestDistance = dist;
      }
    }
  }

  if (!bestPlayer) {
    meta.targetPlayerId = null;
    for (const [playerId, player] of players.entries()) {
      if (playerId === npcId) continue;
      if (stats.getHp(playerId) <= 0) continue;
      const dist = distanceBetween(npcX, npcY, player.x, player.y);
      if (dist <= config.NPC_LOSE_RANGE && dist < bestDistance) {
        bestPlayer = { id: playerId, ...player };
        bestDistance = dist;
      }
    }
  }

  if (bestPlayer) {
    meta.targetPlayerId = bestPlayer.id;
  }

  return { player: bestPlayer, distanceToPlayer: bestDistance };
}

function updateAiState({ meta, npc, player, distanceToPlayer, healthPercent, now, config }) {
  const hasPlayer = !!player;
  const patrolCells = Array.isArray(npc.patrolCells) ? npc.patrolCells : [];
  const canAttack = now >= meta.lastAttackTime + config.NPC_ATTACK_COOLDOWN;

  if (!meta.state) {
    meta.state = patrolCells.length > 0 ? 'Patrol' : 'Idle';
    meta.tStateChange = now;
  }

  switch (meta.state) {
    case 'Idle':
  // если игрок рядом — начинаем преследование даже без patrolCells
  if (hasPlayer && distanceToPlayer <= config.NPC_AGGRO_RANGE) {
    changeState(meta, 'Chase', now);
  } else if (patrolCells.length > 0) {
    changeState(meta, 'Patrol', now);
  }
  break;

    case 'Patrol':
      if (hasPlayer && distanceToPlayer <= config.NPC_AGGRO_RANGE) {
        changeState(meta, 'Chase', now);
      }
      break;
    case 'Chase':
      if (!hasPlayer || distanceToPlayer > config.NPC_LOSE_RANGE) {
        if (meta.lastKnownPlayerCell && now - meta.lastSeenTime < config.NPC_MEMORY_DURATION) {
          changeState(meta, 'Search', now);
        } else {
          changeState(meta, patrolCells.length > 0 ? 'Patrol' : 'Idle', now);
        }
      } else if (distanceToPlayer <= config.NPC_ATTACK_RANGE && canAttack) {
        changeState(meta, 'Attack', now);
      } else if (healthPercent < config.NPC_RETREAT_HEALTH_THRESHOLD && Math.random() < config.NPC_RETREAT_CHANCE) {
        changeState(meta, 'Retreat', now);
      }
      break;
    case 'Attack':
      if (now - meta.tStateChange > config.NPC_ATTACK_COOLDOWN_AFTER_MOVE) {
        if (hasPlayer && distanceToPlayer > config.NPC_ATTACK_RANGE) {
          changeState(meta, 'Chase', now);
        } else if (healthPercent < config.NPC_RETREAT_HEALTH_THRESHOLD) {
          changeState(meta, 'Retreat', now);
        } else if (hasPlayer && distanceToPlayer <= config.NPC_ATTACK_RANGE && canAttack) {
          // stay
        } else {
          changeState(meta, 'Chase', now);
        }
      }
      break;
    case 'Retreat':
      if (!hasPlayer || distanceToPlayer > config.NPC_LOSE_RANGE) {
        changeState(meta, patrolCells.length > 0 ? 'Patrol' : 'Idle', now);
      } else if (
        distanceToPlayer >= config.NPC_RETREAT_DISTANCE &&
        healthPercent > config.NPC_RETREAT_HEALTH_THRESHOLD * 1.5
      ) {
        changeState(meta, 'Chase', now);
      } else if (
        distanceToPlayer <= config.NPC_ATTACK_RANGE &&
        healthPercent > config.NPC_RETREAT_HEALTH_THRESHOLD
      ) {
        changeState(meta, 'Chase', now);
      }
      break;
    case 'Search':
      if (hasPlayer && distanceToPlayer <= config.NPC_AGGRO_RANGE) {
        changeState(meta, 'Chase', now);
      } else if (now - meta.lastSeenTime > config.NPC_MEMORY_DURATION) {
        changeState(meta, patrolCells.length > 0 ? 'Patrol' : 'Idle', now);
      }
      break;
    default:
      changeState(meta, 'Idle', now);
      break;
  }
}

function changeState(meta, next, now) {
  if (meta.state === next) return;
  meta.state = next;
  meta.tStateChange = now;
}

function decideAction({
  meta,
  npcId,
  npc,
  currentCell,
  player,
  distanceToPlayer,
  healthPercent,
  now,
  config,
  occupancy,
  blockedCells,
  stats,
  broadcast,
}) {
  switch (meta.state) {
    case 'Patrol':
      return decidePatrolStep({ meta, npc, currentCell, config, occupancy, blockedCells, now });
    case 'Chase':
      return decideChaseStep({
        meta,
        npc,
        currentCell,
        player,
        distanceToPlayer,
        now,
        config,
        occupancy,
        blockedCells,
      });
    case 'Attack':
      return decideAttackStep({
        meta,
        npcId,
        npc,
        currentCell,
        player,
        config,
        occupancy,
        blockedCells,
        now,
        stats,
        broadcast,
      });
    case 'Retreat':
      return decideRetreatStep({
        meta,
        npc,
        currentCell,
        player,
        config,
        occupancy,
        blockedCells,
      });
    case 'Search':
      return decideSearchStep({ meta, currentCell, config, occupancy, blockedCells });
    case 'Idle':
    default:
      return null;
  }
}

function decidePatrolStep({ meta, npc, currentCell, config, occupancy, blockedCells, now }) {
  const patrolCells = Array.isArray(npc.patrolCells) ? npc.patrolCells : [];
  if (patrolCells.length === 0) return null;

  const patrolIndex = Math.min(meta.patrolIndex || 0, patrolCells.length - 1);
  const target = patrolCells[patrolIndex];
  const targetCell = { x: target.x, y: target.y };

  if (targetCell.x === currentCell.x && targetCell.y === currentCell.y) {
    if (now < meta.patrolWaitUntil) {
      return null;
    }

    const nextIndex = patrolIndex + 1;
    const loop = config.NPC_LOOP_PATROL;
    if (!loop && nextIndex >= patrolCells.length) {
      return null;
    }

    meta.patrolIndex = loop ? nextIndex % patrolCells.length : nextIndex;
    meta.patrolWaitUntil = now + config.NPC_PATROL_WAIT_TIME;
  }

  const newTarget = patrolCells[Math.min(meta.patrolIndex || 0, patrolCells.length - 1)];
  return findPathTo(currentCell, { x: newTarget.x, y: newTarget.y }, occupancy, blockedCells);
}

function decideChaseStep({
  meta,
  npc,
  currentCell,
  player,
  distanceToPlayer,
  now,
  config,
  occupancy,
  blockedCells,
}) {
  if (!player) return null;

  const predicted = predictPlayerPosition(player, config);
  const targetCell = worldToCell(predicted.x, predicted.y, config);

  if (distanceToPlayer <= config.NPC_ATTACK_RANGE) {
    const canAttack = now >= meta.lastAttackTime + config.NPC_ATTACK_COOLDOWN;
    if (canAttack) {
      return null;
    }
  }

  return findPathTo(currentCell, targetCell, occupancy, blockedCells);
}

function decideAttackStep({
  meta,
  npcId,
  npc,
  currentCell,
  player,
  config,
  occupancy,
  blockedCells,
  now,
  stats,
  broadcast,
}) {
  if (!player) return null;

  const playerCell = worldToCell(player.x, player.y, config);
  const dx = playerCell.x - currentCell.x;
  const dy = playerCell.y - currentCell.y;
  const adjacent = Math.abs(dx) <= 1 && Math.abs(dy) <= 1 && (dx !== 0 || dy !== 0);
  const canAttack = now >= meta.lastAttackTime + config.NPC_ATTACK_COOLDOWN;
  const canAttackAfterMove = now >= meta.lastMoveTime + config.NPC_ATTACK_COOLDOWN_AFTER_MOVE;

  if (adjacent && canAttack && canAttackAfterMove) {
    const dir = normalizeVector(player.x - npc.x, player.y - npc.y);
    performAttack({
      npcId,
      playerId: player.id,
      dirX: dir.x,
      dirY: dir.y,
      player,
      stats,
      broadcast,
      config,
    });
    meta.lastAttackTime = now;
    return null;
  }

  return findPathTo(currentCell, playerCell, occupancy, blockedCells);
}

function decideRetreatStep({ currentCell, player, config, occupancy, blockedCells }) {
  if (!player) return null;

  const playerCell = worldToCell(player.x, player.y, config);
  const retreatDirection = {
    x: currentCell.x - playerCell.x,
    y: currentCell.y - playerCell.y,
  };
  const rx = retreatDirection.x === 0 ? 0 : retreatDirection.x > 0 ? 1 : -1;
  const ry = retreatDirection.y === 0 ? 0 : retreatDirection.y > 0 ? 1 : -1;
  const retreatTarget = { x: currentCell.x + rx, y: currentCell.y + ry };

  if (!isCellWalkable(retreatTarget, currentCell, occupancy, blockedCells)) {
    const alternatives = [
      { x: currentCell.x + ry, y: currentCell.y + rx },
      { x: currentCell.x - ry, y: currentCell.y - rx },
      { x: currentCell.x + rx, y: currentCell.y },
      { x: currentCell.x, y: currentCell.y + ry },
    ];

    for (const alt of alternatives) {
      if (isCellWalkable(alt, currentCell, occupancy, blockedCells)) {
        return alt;
      }
    }
  }

  return isCellWalkable(retreatTarget, currentCell, occupancy, blockedCells) ? retreatTarget : null;
}

function decideSearchStep({ meta, currentCell, occupancy, blockedCells }) {
  if (!meta.lastKnownPlayerCell) return null;

  const target = meta.lastKnownPlayerCell;
  if (target.x === currentCell.x && target.y === currentCell.y) {
    for (const dir of DIRECTIONS) {
      const checkCell = { x: currentCell.x + dir.x, y: currentCell.y + dir.y };
      if (isCellWalkable(checkCell, currentCell, occupancy, blockedCells)) {
        return checkCell;
      }
    }
    return null;
  }

  return findPathTo(currentCell, target, occupancy, blockedCells);
}

function findPathTo(currentCell, targetCell, occupancy, blockedCells) {
  const directStep = stepTowards(currentCell, targetCell);
  if (isCellWalkable(directStep, currentCell, occupancy, blockedCells)) {
    return directStep;
  }

  const dx = targetCell.x - currentCell.x;
  const dy = targetCell.y - currentCell.y;

  let alternatives;
  if (Math.abs(dx) > Math.abs(dy)) {
    alternatives = [
      { x: currentCell.x + (dx > 0 ? 1 : -1), y: currentCell.y },
      { x: currentCell.x, y: currentCell.y + (dy > 0 ? 1 : -1) },
      { x: currentCell.x + (dx > 0 ? 1 : -1), y: currentCell.y + (dy > 0 ? 1 : -1) },
      { x: currentCell.x + (dx > 0 ? 1 : -1), y: currentCell.y - (dy > 0 ? 1 : -1) },
    ];
  } else {
    alternatives = [
      { x: currentCell.x, y: currentCell.y + (dy > 0 ? 1 : -1) },
      { x: currentCell.x + (dx > 0 ? 1 : -1), y: currentCell.y },
      { x: currentCell.x + (dx > 0 ? 1 : -1), y: currentCell.y + (dy > 0 ? 1 : -1) },
      { x: currentCell.x - (dx > 0 ? 1 : -1), y: currentCell.y + (dy > 0 ? 1 : -1) },
    ];
  }

  for (const alt of alternatives) {
    if (isCellWalkable(alt, currentCell, occupancy, blockedCells)) {
      return alt;
    }
  }

  for (const dir of DIRECTIONS) {
    const checkCell = { x: currentCell.x + dir.x, y: currentCell.y + dir.y };
    if (isCellWalkable(checkCell, currentCell, occupancy, blockedCells)) {
      return checkCell;
    }
  }

  return null;
}

function isCellWalkable(cell, currentCell, occupancy, blockedCells) {
  if (!cell) return false;
  if (cell.x === currentCell.x && cell.y === currentCell.y) return false;
  const key = `${cell.x},${cell.y}`;
  if (blockedCells.has(key)) return false;
  if (occupancy.has(key)) return false;
  return true;
}

function stepTowards(currentCell, targetCell) {
  const dx = targetCell.x - currentCell.x;
  const dy = targetCell.y - currentCell.y;
  const sx = dx === 0 ? 0 : dx > 0 ? 1 : -1;
  const sy = dy === 0 ? 0 : dy > 0 ? 1 : -1;
  return { x: currentCell.x + sx, y: currentCell.y + sy };
}

function worldToCell(x, y, config) {
  const fx = x / config.GRID_SIZE - config.CELL_CENTER_OFFSET_X;
  const fy = y / config.GRID_SIZE - config.CELL_CENTER_OFFSET_Y;
  return { x: Math.round(fx), y: Math.round(fy) };
}

function cellToWorld(cell, config) {
  return {
    x: (cell.x + config.CELL_CENTER_OFFSET_X) * config.GRID_SIZE,
    y: (cell.y + config.CELL_CENTER_OFFSET_Y) * config.GRID_SIZE,
  };
}

function predictPlayerPosition(player, config) {
  const vx = typeof player.vx === 'number' ? player.vx : 0;
  const vy = typeof player.vy === 'number' ? player.vy : 0;
  return {
    x: player.x + vx * config.NPC_PREDICTION_TIME,
    y: player.y + vy * config.NPC_PREDICTION_TIME,
  };
}

function distanceBetween(x1, y1, x2, y2) {
  const dx = x2 - x1;
  const dy = y2 - y1;
  return Math.sqrt(dx * dx + dy * dy);
}

function normalizeVector(x, y) {
  const len = Math.hypot(x, y) || 1;
  return { x: x / len, y: y / len };
}

function performAttack({ npcId, playerId, dirX, dirY, player, stats, broadcast, config }) {
  const oldHp = stats.getHp(playerId);
  const newHp = stats.setHp(playerId, oldHp - config.NPC_ATTACK_DAMAGE);
  const appliedDamage = Math.max(0, oldHp - newHp);

  const popupX = player && typeof player.x === 'number' ? player.x : 0;
  const popupY = player && typeof player.y === 'number' ? player.y : 0;

  broadcast({
    type: 'npc_attack',
    npcId,
    targetId: playerId,
    dirX,
    dirY,
  });

  const evt = {
    type: 'damage',
    sourceId: npcId,
    targetId: playerId,
    amount: appliedDamage,
    hp: newHp,
  };

  if (appliedDamage > 0) {
    const popupMsg = {
      type: 'damage_popup',
      amount: Math.round(appliedDamage),
      x: popupX,
      y: popupY,
      z: 0,
    };
    broadcast(popupMsg);

    const hitFxMsg = {
      type: 'hit_fx',
      fx: 'claws',
      targetId: playerId,
      zone: '',
      x: popupX,
      y: popupY,
      z: 0,
    };
    broadcast(hitFxMsg);
  }

  broadcast(evt);
}

module.exports = {
  startNpcAiLoop,
};
