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

function spawnNpcsIfNeeded({ broadcast, setHp, defaultHp, spawnPoints }) {
  if (npcStates.size > 0) return;

  spawnPoints.forEach((spawn) => {
    const hp = setHp(spawn.id, defaultHp);
    const npcState = { x: spawn.x, y: spawn.y, hp };
    npcStates.set(spawn.id, npcState);
    const meta = ensureNpcMeta(spawn.id);

    broadcast({
      type: 'npc_spawn',
      npcId: spawn.id,
      x: spawn.x,
      y: spawn.y,
      hp,
      state: meta.state,
      targetId: meta.targetPlayerId,
      dirX: meta.dirX,
      dirY: meta.dirY,
      moving: !!meta.moving,
    });
  });
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
