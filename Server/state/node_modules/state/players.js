const playerStates = new Map();

function getPlayer(id) {
  return playerStates.get(id);
}

function setPlayer(id, state) {
  playerStates.set(id, state);
}

function removePlayer(id) {
  playerStates.delete(id);
}

function entries() {
  return playerStates.entries();
}

module.exports = {
  getPlayer,
  setPlayer,
  removePlayer,
  entries,
};
