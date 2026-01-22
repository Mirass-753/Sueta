const preyStates = new Map();
const preyByOwner = new Map();

function registerPrey({ id, ownerId, x, y, dropItemName }) {
  if (!id) return null;
  const state = {
    id,
    ownerId: ownerId || null,
    x: typeof x === 'number' ? x : 0,
    y: typeof y === 'number' ? y : 0,
    dropItemName: dropItemName || null,
  };
  preyStates.set(id, state);
  if (ownerId) preyByOwner.set(ownerId, id);
  return state;
}

function updatePreyPosition(id, x, y) {
  const state = preyStates.get(id);
  if (!state) return null;
  state.x = typeof x === 'number' ? x : state.x;
  state.y = typeof y === 'number' ? y : state.y;
  return state;
}

function getPrey(id) {
  return preyStates.get(id);
}

function getPreyByOwner(ownerId) {
  if (!ownerId) return null;
  const id = preyByOwner.get(ownerId);
  return id ? preyStates.get(id) : null;
}

function removePrey(id) {
  const state = preyStates.get(id);
  if (!state) return null;
  preyStates.delete(id);
  if (state.ownerId && preyByOwner.get(state.ownerId) === id) {
    preyByOwner.delete(state.ownerId);
  }
  return state;
}

function removePreyByOwner(ownerId) {
  const state = getPreyByOwner(ownerId);
  if (!state) return null;
  return removePrey(state.id);
}

function entries() {
  return preyStates.entries();
}

module.exports = {
  registerPrey,
  updatePreyPosition,
  getPrey,
  getPreyByOwner,
  removePrey,
  removePreyByOwner,
  entries,
};
