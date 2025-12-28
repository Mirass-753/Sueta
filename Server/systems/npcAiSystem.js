const fs = require('fs');
const path = require('path');

const ORTHO_DIRECTIONS = [
  { x: 1, y: 0 },
  { x: -1, y: 0 },
  { x: 0, y: 1 },
  { x: 0, y: -1 },
];
const DIAGONAL_DIRECTIONS = [
  { x: 1, y: 1 },
  { x: 1, y: -1 },
  { x: -1, y: 1 },
  { x: -1, y: -1 },
];
const DIRECTIONS = [...ORTHO_DIRECTIONS, ...DIAGONAL_DIRECTIONS];
const MAX_PATH_NODES = 200;
const ORTHO_COST = 1;
const DIAGONAL_COST = Math.SQRT2;
const DEBUG_AI = String(process.env.DEBUG_AI || '').toLowerCase() === 'true'
  || process.env.DEBUG_AI === '1';

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
      meta.npcId = npcId;
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
        if (DEBUG_AI) {
          console.log('[NPC AI] move', npcId, {
            from: currentCell,
            to: nextCell,
          });
        }
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
        if (DEBUG_AI && meta.moving) {
          console.log('[NPC AI] stop', npcId, { at: currentCell });
        }
        meta.moving = false;
        meta.dirX = 0;
        meta.dirY = 0;
      }

      if (shouldSendNpcState(meta, npc)) {
        sendNpcStateToClient(npc, meta, broadcast);
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
  if (typeof meta.attackWindowUntil !== 'number') {
    meta.attackWindowUntil = -Infinity;
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
  if (typeof meta.lastSentX !== 'number') {
    meta.lastSentX = NaN;
  }
  if (typeof meta.lastSentY !== 'number') {
    meta.lastSentY = NaN;
  }
  if (typeof meta.lastSentHp !== 'number') {
    meta.lastSentHp = NaN;
  }
  if (typeof meta.lastSentDirX !== 'number') {
    meta.lastSentDirX = NaN;
  }
  if (typeof meta.lastSentDirY !== 'number') {
    meta.lastSentDirY = NaN;
  }
  if (typeof meta.lastSentMoving !== 'boolean') {
    meta.lastSentMoving = null;
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
  const currentCell = worldToCell(npc.x, npc.y, config);
  const playerCell = player ? worldToCell(player.x, player.y, config) : null;
  const attackDir = player ? getAttackDirection(meta, npc, player) : { x: 0, y: 0 };
  const hasAttackContact = player
    ? isAttackHit({ npc, player, dirX: attackDir.x, dirY: attackDir.y, config })
    : false;

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
      } else if (hasAttackContact) {
        changeState(meta, 'Attack', now);
      } else if (healthPercent < config.NPC_RETREAT_HEALTH_THRESHOLD && Math.random() < config.NPC_RETREAT_CHANCE) {
        changeState(meta, 'Retreat', now);
      }
      break;
    case 'Attack':
      if (now - meta.tStateChange > config.NPC_ATTACK_COOLDOWN_AFTER_MOVE) {
        if (hasPlayer && !hasAttackContact) {
          changeState(meta, 'Chase', now);
        } else if (healthPercent < config.NPC_RETREAT_HEALTH_THRESHOLD) {
          changeState(meta, 'Retreat', now);
        } else if (hasPlayer && hasAttackContact && canAttack) {
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
  if (DEBUG_AI) {
    console.log('[NPC AI] state', meta.npcId || '?', meta.state, '->', next);
  }
  meta.state = next;
  meta.tStateChange = now;
}

function areCellsAdjacent(a, b) {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  return Math.abs(dx) <= 1 && Math.abs(dy) <= 1 && (dx !== 0 || dy !== 0);
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
  if (DEBUG_AI) {
    console.log('[NPC AI] decide', meta.npcId || '?', {
      state: meta.state,
      currentCell,
      distanceToPlayer,
      healthPercent,
    });
  }
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
  if (DEBUG_AI) {
    console.log('[NPC AI] patrol', meta.npcId || '?', {
      targetCell: newTarget,
      patrolIndex: meta.patrolIndex || 0,
    });
  }
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

  const attackDir = getAttackDirection(meta, npc, player);
  const hasAttackContact = isAttackHit({ npc, player, dirX: attackDir.x, dirY: attackDir.y, config });
  const npcWorld = cellToWorld(currentCell, config);
  const snapFactor = typeof config.NPC_ALIGN_SNAP_FACTOR === 'number'
    ? config.NPC_ALIGN_SNAP_FACTOR
    : 0.25;
  const snapThreshold = config.GRID_SIZE * snapFactor;
  const dampenX = Math.abs(player.x - npcWorld.x) <= snapThreshold;
  const dampenY = Math.abs(player.y - npcWorld.y) <= snapThreshold;
  let predictedInput = player;

  if (dampenX || dampenY) {
    let vx = typeof player.vx === 'number' ? player.vx : 0;
    let vy = typeof player.vy === 'number' ? player.vy : 0;
    if (dampenX) {
      vx = 0;
    }
    if (dampenY) {
      vy = 0;
    }
    predictedInput = { ...player, vx, vy };
    if (DEBUG_AI) {
      console.log('[NPC AI] prediction damp', meta.npcId || '?', {
        dampenX,
        dampenY,
        threshold: snapThreshold,
      });
    }
  }

  const predicted = predictPlayerPosition(predictedInput, config);
  let targetCell = worldToCell(predicted.x, predicted.y, config);
  const closeX = Math.abs(predicted.x - npcWorld.x) <= snapThreshold;
  const closeY = Math.abs(predicted.y - npcWorld.y) <= snapThreshold;

  if (closeX !== closeY) {
    if (closeX) {
      targetCell = { x: currentCell.x, y: targetCell.y };
    }
    if (closeY) {
      targetCell = { x: targetCell.x, y: currentCell.y };
    }
  }

  if (hasAttackContact) {
    const canAttack = now >= meta.lastAttackTime + config.NPC_ATTACK_COOLDOWN;
    if (canAttack) {
      if (DEBUG_AI) {
        console.log('[NPC AI] chase hold', meta.npcId || '?', {
          distanceToPlayer,
          attackRange: config.NPC_ATTACK_RANGE,
        });
      }
      return null;
    }
  }

  if (DEBUG_AI) {
    console.log('[NPC AI] chase', meta.npcId || '?', { targetCell });
  }
  return findPathTo(currentCell, targetCell, occupancy, blockedCells, { forceOrtho: closeX || closeY });
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
  const canAttack = now >= meta.lastAttackTime + config.NPC_ATTACK_COOLDOWN;
  const canAttackAfterMove = now >= meta.lastMoveTime + config.NPC_ATTACK_COOLDOWN_AFTER_MOVE;
  const dir = getAttackDirection(meta, npc, player);
  const hasValidDirection = isAttackDirectionValid({ npc, player, dirX: dir.x, dirY: dir.y, config });
  const hasValidHit = isAttackHit({ npc, player, dirX: dir.x, dirY: dir.y, config });

  if (hasValidHit && canAttack && canAttackAfterMove) {
    if (DEBUG_AI) {
      console.log('[NPC AI] attack', meta.npcId || '?', {
        targetId: player.id,
        dirX: dir.x,
        dirY: dir.y,
        hasValidDirection,
        hasValidHit,
      });
    }
    if (hasValidDirection && hasValidHit) {
      performAttack({
        npcId,
        npc,
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
  }

  if (hasValidHit) {
    if (DEBUG_AI) {
      console.log('[NPC AI] attack wait', meta.npcId || '?', {
        canAttack,
        canAttackAfterMove,
      });
    }
    meta.dirX = dir.x;
    meta.dirY = dir.y;
    meta.attackWindowUntil = now + (typeof config.NPC_ATTACK_WINDOW_SECONDS === 'number'
      ? config.NPC_ATTACK_WINDOW_SECONDS
      : 0.2);
    broadcast({
      type: 'npc_attack',
      npcId,
      targetId: player.id,
      dirX: dir.x,
      dirY: dir.y,
    });
    meta.lastAttackTime = now;
    return null;
  }

  if (DEBUG_AI) {
    console.log('[NPC AI] attack move', meta.npcId || '?', { playerCell });
  }
  return findPathTo(currentCell, playerCell, occupancy, blockedCells);
}

function getAttackDirection(meta, npc, player) {
  const fallback = normalizeVector(player.x - npc.x, player.y - npc.y);
  const hasFacing = typeof meta.dirX === 'number'
    && typeof meta.dirY === 'number'
    && (meta.dirX !== 0 || meta.dirY !== 0);
  if (!hasFacing) {
    return fallback;
  }
  const facing = normalizeVector(meta.dirX, meta.dirY);
  return facing.x === 0 && facing.y === 0 ? fallback : facing;
}

function isAttackDirectionValid({ npc, player, dirX, dirY, config }) {
  const dirLen = Math.hypot(dirX, dirY);
  if (!npc || !player || dirLen === 0) return false;
  const toPlayer = normalizeVector(player.x - npc.x, player.y - npc.y);
  const dot = (dirX * toPlayer.x + dirY * toPlayer.y) / dirLen;
  return dot >= config.NPC_ATTACK_FACING_DOT;
}

function isAttackHit({ npc, player, dirX, dirY, config }) {
  if (!npc || !player) return false;
  const len = Math.hypot(dirX, dirY);
  if (len === 0) return false;

  const right = { x: dirX / len, y: dirY / len };
  const up = { x: -right.y, y: right.x };
  const halfX = config.NPC_ARROW_HITBOX_SIZE_X / 2;
  const halfY = config.NPC_ARROW_HITBOX_SIZE_Y / 2;
  const offsetX = config.NPC_ARROW_HITBOX_OFFSET_X;
  const offsetY = config.NPC_ARROW_HITBOX_OFFSET_Y;

  const center = {
    x: npc.x + right.x * offsetX + up.x * offsetY,
    y: npc.y + right.y * offsetX + up.y * offsetY,
  };

  const playerHalfX = config.PLAYER_HITBOX_SIZE_X / 2;
  const playerHalfY = config.PLAYER_HITBOX_SIZE_Y / 2;
  const playerCenter = {
    x: player.x + config.PLAYER_HITBOX_OFFSET_X,
    y: player.y + config.PLAYER_HITBOX_OFFSET_Y,
  };

  return overlapObbAabb(center, halfX, halfY, right, up, playerCenter, playerHalfX, playerHalfY);
}

function overlapObbAabb(obbCenter, obbHalfX, obbHalfY, obbRight, obbUp, aabbCenter, aabbHalfX, aabbHalfY) {
  const axes = [
    obbRight,
    obbUp,
    { x: 1, y: 0 },
    { x: 0, y: 1 },
  ];
  const delta = {
    x: aabbCenter.x - obbCenter.x,
    y: aabbCenter.y - obbCenter.y,
  };

  for (const axis of axes) {
    const axisLen = Math.hypot(axis.x, axis.y) || 1;
    const ax = axis.x / axisLen;
    const ay = axis.y / axisLen;
    const distance = Math.abs(delta.x * ax + delta.y * ay);
    const obbRadius = obbHalfX * Math.abs(ax * obbRight.x + ay * obbRight.y)
      + obbHalfY * Math.abs(ax * obbUp.x + ay * obbUp.y);
    const aabbRadius = aabbHalfX * Math.abs(ax) + aabbHalfY * Math.abs(ay);
    if (distance > obbRadius + aabbRadius) {
      return false;
    }
  }

  return true;
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
  if (DEBUG_AI) {
    console.log('[NPC AI] retreat', {
      playerCell,
      retreatTarget,
    });
  }

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
  if (DEBUG_AI) {
    console.log('[NPC AI] search', meta.npcId || '?', { target });
  }
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

function findPathTo(currentCell, targetCell, occupancy, blockedCells, options = {}) {
  if (!targetCell) return null;
  const maxNodes = MAX_PATH_NODES;
  const startKey = cellKey(currentCell);
  const goalKey = cellKey(targetCell);
  if (startKey === goalKey) return null;
  const aligned = currentCell.x === targetCell.x || currentCell.y === targetCell.y;
  const neighborDirections = aligned || options.forceOrtho ? ORTHO_DIRECTIONS : DIRECTIONS;

  let goalKeys = [];
  if (isCellWalkable(targetCell, currentCell, occupancy, blockedCells)) {
    goalKeys = [goalKey];
  } else {
    const candidateGoals = getNeighbors(targetCell, neighborDirections).filter((cell) =>
      isCellWalkable(cell, currentCell, occupancy, blockedCells),
    );
    goalKeys = candidateGoals.map((cell) => cellKey(cell));
  }

  if (goalKeys.length === 0) {
    if (DEBUG_AI) {
      console.log('[NPC AI] path none', { currentCell, targetCell });
    }
    return null;
  }
  const goalSet = new Set(goalKeys);

  const open = [{
    cell: currentCell,
    key: startKey,
    g: 0,
    f: heuristicCost(currentCell, targetCell),
    isDiagonal: false,
  }];
  const cameFrom = new Map();
  const gScore = new Map([[startKey, 0]]);
  const closed = new Set();
  let reachedGoalKey = null;

  while (open.length > 0 && closed.size <= maxNodes) {
    let hasTieBreak = false;
    if (DEBUG_AI && open.length > 1) {
      for (let i = 0; i < open.length - 1 && !hasTieBreak; i += 1) {
        const item = open[i];
        for (let j = i + 1; j < open.length; j += 1) {
          const other = open[j];
          if (item.f === other.f && item.g === other.g && item.isDiagonal !== other.isDiagonal) {
            hasTieBreak = true;
            break;
          }
        }
      }
    }
    open.sort((a, b) => (
      a.f - b.f
      || a.g - b.g
      || (a.isDiagonal === b.isDiagonal ? 0 : (a.isDiagonal ? 1 : -1))
    ));
    if (DEBUG_AI && hasTieBreak) {
      console.log('[NPC AI] path tie-break', { currentCell, targetCell });
    }
    const current = open.shift();
    if (!current) break;
    if (goalSet.has(current.key)) {
      reachedGoalKey = current.key;
      break;
    }

    closed.add(current.key);

    const neighbors = getNeighbors(current.cell, neighborDirections);
    for (const next of neighbors) {
      const nextKey = cellKey(next);
      if (closed.has(nextKey)) continue;
      if (!isCellWalkable(next, currentCell, occupancy, blockedCells)) continue;
      const stepCost = movementCost(current.cell, next);
      const tentativeG = current.g + stepCost;
      const knownG = gScore.get(nextKey);
      if (knownG !== undefined && tentativeG >= knownG) continue;

      cameFrom.set(nextKey, current.key);
      gScore.set(nextKey, tentativeG);
      const fScore = tentativeG + heuristicCost(next, targetCell);
      const existingIndex = open.findIndex((item) => item.key === nextKey);
      const dx = Math.abs(next.x - current.cell.x);
      const dy = Math.abs(next.y - current.cell.y);
      const isDiagonal = dx === 1 && dy === 1;
      if (existingIndex >= 0) {
        open[existingIndex] = {
          cell: next,
          key: nextKey,
          g: tentativeG,
          f: fScore,
          isDiagonal,
        };
      } else {
        open.push({
          cell: next,
          key: nextKey,
          g: tentativeG,
          f: fScore,
          isDiagonal,
        });
      }
    }
  }

  if (!reachedGoalKey) {
    if (DEBUG_AI) {
      console.log('[NPC AI] path failed', { currentCell, targetCell });
    }
    return null;
  }

  let stepKey = reachedGoalKey;
  let previousKey = cameFrom.get(stepKey);
  while (previousKey && previousKey !== startKey) {
    stepKey = previousKey;
    previousKey = cameFrom.get(stepKey);
  }

  if (!previousKey && stepKey !== reachedGoalKey) {
    return null;
  }

  const nextStep = parseCellKey(stepKey);
  if (DEBUG_AI) {
    console.log('[NPC AI] path step', { currentCell, targetCell, nextStep });
  }
  return isCellWalkable(nextStep, currentCell, occupancy, blockedCells) ? nextStep : null;
}

function isCellWalkable(cell, currentCell, occupancy, blockedCells) {
  if (!cell) return false;
  if (cell.x === currentCell.x && cell.y === currentCell.y) return false;
  const key = `${cell.x},${cell.y}`;
  if (blockedCells.has(key)) return false;
  if (occupancy.has(key)) return false;
  return true;
}

function getNeighbors(cell, directions = DIRECTIONS) {
  return directions.map((dir) => ({ x: cell.x + dir.x, y: cell.y + dir.y }));
}

function cellKey(cell) {
  return `${cell.x},${cell.y}`;
}

function parseCellKey(key) {
  const [x, y] = key.split(',').map(Number);
  return { x, y };
}

function movementCost(fromCell, toCell) {
  const dx = Math.abs(toCell.x - fromCell.x);
  const dy = Math.abs(toCell.y - fromCell.y);
  return dx === 1 && dy === 1 ? DIAGONAL_COST : ORTHO_COST;
}

function heuristicCost(cell, targetCell) {
  const dx = Math.abs(targetCell.x - cell.x);
  const dy = Math.abs(targetCell.y - cell.y);
  const min = Math.min(dx, dy);
  const max = Math.max(dx, dy);
  return DIAGONAL_COST * min + ORTHO_COST * (max - min);
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
  let vx = typeof player.vx === 'number' ? player.vx : 0;
  let vy = typeof player.vy === 'number' ? player.vy : 0;
  const absVx = Math.abs(vx);
  const absVy = Math.abs(vy);

  if (absVx > 0 && absVy > 0) {
    const ratio = Math.min(absVx, absVy) / Math.max(absVx, absVy);
    if (ratio >= 0.9) {
      return { x: player.x, y: player.y };
    }
  }

  if (absVx > absVy) {
    vy = 0;
  } else if (absVy > absVx) {
    vx = 0;
  }
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

function shouldSendNpcState(meta, npc) {
  const keys = ['lastSentX', 'lastSentY', 'lastSentHp', 'lastSentDirX', 'lastSentDirY', 'lastSentMoving'];
  return [npc.x, npc.y, npc.hp, meta.dirX, meta.dirY, meta.moving].some(
    (value, index) => value !== meta[keys[index]],
  );
}

function sendNpcStateToClient(npc, meta, broadcast) {
  const state = {
    npcId: npc.id,
    x: npc.x,
    y: npc.y,
    hp: npc.hp,
    dirX: meta.dirX,
    dirY: meta.dirY,
    moving: meta.moving,
  };

  meta.lastSentX = npc.x;
  meta.lastSentY = npc.y;
  meta.lastSentHp = npc.hp;
  meta.lastSentDirX = meta.dirX;
  meta.lastSentDirY = meta.dirY;
  meta.lastSentMoving = meta.moving;

  broadcast({ type: 'npc_state', state });
}

module.exports = {
  startNpcAiLoop,
};
