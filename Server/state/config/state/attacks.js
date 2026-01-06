const attacks = new Map();

function makeAttackId() {
  return `atk-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

function createAttack({ sourceId, targetId = null, dirX = 0, dirY = 0, weapon = 'claws', windowSeconds = 0.2 }) {
  const attackId = makeAttackId();
  const now = Date.now() / 1000;
  attacks.set(attackId, {
    attackId,
    sourceId,
    targetId,
    dirX,
    dirY,
    weapon,
    startTime: now,
    windowSeconds,
  });
  return attackId;
}

function getAttack(attackId) {
  return attacks.get(attackId) || null;
}

function removeAttack(attackId) {
  attacks.delete(attackId);
}

function isAttackActive(attack, now = Date.now() / 1000) {
  if (!attack) return false;
  return now <= attack.startTime + attack.windowSeconds;
}

module.exports = {
  createAttack,
  getAttack,
  removeAttack,
  isAttackActive,
};
