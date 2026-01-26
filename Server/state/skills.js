const config = require('../config/constants');

const skillDefs = {
  sniff: {
    id: 'sniff',
    name: 'Нюх',
    maxLevel: config.SNIFF_MAX_LEVEL,
    expPerUse: config.SNIFF_EXP_PER_USE,
    expPerLevel: config.SNIFF_EXP_PER_LEVEL,
    cooldownSeconds: config.SNIFF_COOLDOWN_SECONDS,
  },
};

const playerSkills = new Map();

function ensurePlayerSkills(playerId) {
  if (!playerSkills.has(playerId)) {
    playerSkills.set(playerId, new Map());
  }
  return playerSkills.get(playerId);
}

function ensureSkill(playerId, skillId) {
  const skills = ensurePlayerSkills(playerId);
  if (!skills.has(skillId)) {
    skills.set(skillId, {
      level: 1,
      exp: 0,
      lastUseAt: -Infinity,
    });
  }
  return skills.get(skillId);
}

function getSkillDef(skillId) {
  return skillDefs[skillId];
}

function canUseSkill(playerId, skillId, nowSeconds) {
  const def = getSkillDef(skillId);
  if (!def) return false;

  const state = ensureSkill(playerId, skillId);
  const cooldown = typeof def.cooldownSeconds === 'number' ? def.cooldownSeconds : 0;
  return nowSeconds - state.lastUseAt >= cooldown;
}

function applySkillUse(playerId, skillId, nowSeconds) {
  const def = getSkillDef(skillId);
  if (!def) return null;

  const state = ensureSkill(playerId, skillId);
  state.lastUseAt = nowSeconds;

  if (state.level >= def.maxLevel) {
    state.level = def.maxLevel;
    state.exp = 0;
    return state;
  }

  const expPerUse = Math.max(0, def.expPerUse);
  const expPerLevel = Math.max(0.01, def.expPerLevel);

  state.exp += expPerUse;
  while (state.exp >= expPerLevel && state.level < def.maxLevel) {
    state.exp -= expPerLevel;
    state.level += 1;
  }

  if (state.level >= def.maxLevel) {
    state.level = def.maxLevel;
    state.exp = 0;
  }

  return state;
}

function applySkillExp(playerId, skillId) {
  const def = getSkillDef(skillId);
  if (!def) return null;

  const state = ensureSkill(playerId, skillId);

  if (state.level >= def.maxLevel) {
    state.level = def.maxLevel;
    state.exp = 0;
    return state;
  }

  const expPerUse = Math.max(0, def.expPerUse);
  const expPerLevel = Math.max(0.01, def.expPerLevel);

  state.exp += expPerUse;
  while (state.exp >= expPerLevel && state.level < def.maxLevel) {
    state.exp -= expPerLevel;
    state.level += 1;
  }

  if (state.level >= def.maxLevel) {
    state.level = def.maxLevel;
    state.exp = 0;
  }

  return state;
}

function markSkillUse(playerId, skillId, nowSeconds) {
  const def = getSkillDef(skillId);
  if (!def) return null;

  const state = ensureSkill(playerId, skillId);
  state.lastUseAt = nowSeconds;
  return state;
}

function getSkillSnapshot(playerId, skillId) {
  const def = getSkillDef(skillId);
  if (!def) return null;

  const state = ensureSkill(playerId, skillId);
  return {
    playerId,
    skillId: def.id,
    skillName: def.name,
    level: state.level,
    maxLevel: def.maxLevel,
    exp: state.exp,
    expToLevel: def.expPerLevel,
  };
}

function getPlayerSnapshots(playerId) {
  return Object.keys(skillDefs)
    .map((skillId) => getSkillSnapshot(playerId, skillId))
    .filter(Boolean);
}

function clearPlayer(playerId) {
  playerSkills.delete(playerId);
}

module.exports = {
  skillDefs,
  ensureSkill,
  canUseSkill,
  applySkillUse,
  applySkillExp,
  markSkillUse,
  getSkillSnapshot,
  getPlayerSnapshots,
  clearPlayer,
};
