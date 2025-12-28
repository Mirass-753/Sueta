const npcStates = new Map();
const npcMeta = new Map();

function getNpc(id) {
  return npcStates.get(id);
}

function setNpc(id, state) {
  npcStates.set(id, state);
}

function deleteNpc(id) {
  npcStates.delete(id);
  npcMeta.delete(id);
}

function hasNpc(id) {
  return npcStates.has(id);
}

function entries() {
  return npcStates.entries();
}

function size() {
  return npcStates.size;
}

function getNpcMeta(id) {
  return npcMeta.get(id);
}

function ensureNpcMeta(id) {
  let meta = npcMeta.get(id);
  if (!meta) {
    const now = Date.now() / 1000;
    meta = {
      state: 'Idle',
      tStateChange: now,
      patrolIndex: 0,
      patrolWaitUntil: 0,
      lastKnownPlayerCell: null,
      lastSeenTime: 0,
      lastAttackTime: -Infinity,
      attackWindowUntil: -Infinity,
      lastMoveTime: -Infinity,
      targetPlayerId: null,
      dirX: 0,
      dirY: 0,
      moving: false,
    };
    npcMeta.set(id, meta);
  }
  return meta;
}

function setNpcMeta(id, meta) {
  npcMeta.set(id, meta);
}

function spawnNpcsIfNeeded({ broadcast, setHp, defaultHp, spawnPoints, players, config }) {
  if (npcStates.size > 0) return;

  const playerPos = getFirstPlayerPosition(players);
  const spawnRadius =
    config && typeof config.NPC_SPAWN_NEAR_PLAYER_RADIUS === 'number'
      ? config.NPC_SPAWN_NEAR_PLAYER_RADIUS
      : null;

  spawnPoints.forEach((spawn) => {
    const hp = setHp(spawn.id, defaultHp);
    const spawnPos = playerPos
      ? offsetFromPlayer(playerPos, spawn, spawnRadius)
      : { x: spawn.x, y: spawn.y };
    const npcState = { x: spawnPos.x, y: spawnPos.y, hp };
    npcStates.set(spawn.id, npcState);
    const meta = ensureNpcMeta(spawn.id);

    broadcast({
      type: 'npc_spawn',
      npcId: spawn.id,
      x: spawnPos.x,
      y: spawnPos.y,
      hp,
      state: meta.state,
      targetId: meta.targetPlayerId,
      dirX: meta.dirX,
      dirY: meta.dirY,
      moving: !!meta.moving,
    });
  });
}

function getFirstPlayerPosition(players) {
  if (!players || typeof players.entries !== 'function') return null;
  for (const [, player] of players.entries()) {
    if (player && typeof player.x === 'number' && typeof player.y === 'number') {
      return { x: player.x, y: player.y };
    }
  }
  return null;
}

function offsetFromPlayer(playerPos, spawn, spawnRadius) {
  const fallback = spawnRadius && spawnRadius > 0 ? spawnRadius : 2;
  const rawX = typeof spawn.x === 'number' ? spawn.x : fallback;
  const rawY = typeof spawn.y === 'number' ? spawn.y : 0;
  const length = Math.hypot(rawX, rawY) || 1;
  const targetRadius = spawnRadius && spawnRadius > 0 ? spawnRadius : fallback;
  const scale = targetRadius / length;
  return {
    x: playerPos.x + rawX * scale,
    y: playerPos.y + rawY * scale,
  };
}

module.exports = {
  getNpc,
  setNpc,
  deleteNpc,
  hasNpc,
  entries,
  size,
  getNpcMeta,
  ensureNpcMeta,
  setNpcMeta,
  spawnNpcsIfNeeded,
};
