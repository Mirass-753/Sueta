const npcStates = new Map();

function getNpc(id) {
  return npcStates.get(id);
}

function setNpc(id, state) {
  npcStates.set(id, state);
}

function deleteNpc(id) {
  npcStates.delete(id);
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

function spawnNpcsIfNeeded({ broadcast, setHp, defaultHp, spawnPoints }) {
  if (npcStates.size > 0) return;

  spawnPoints.forEach((spawn) => {
    const hp = setHp(spawn.id, defaultHp);
    const npcState = { x: spawn.x, y: spawn.y, hp };
    npcStates.set(spawn.id, npcState);

    broadcast({
      type: 'npc_spawn',
      npcId: spawn.id,
      x: spawn.x,
      y: spawn.y,
      hp,
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
  spawnNpcsIfNeeded,
};
