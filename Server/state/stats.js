const config = require('../config/constants');

const entityHp = new Map();
const entityEnergy = new Map();
const energyMeta = new Map();

function getHp(id) {
  if (!entityHp.has(id)) {
    entityHp.set(id, config.DEFAULT_HP);
  }
  return entityHp.get(id);
}

function setHp(id, hp) {
  const clamped = Math.max(0, hp);
  entityHp.set(id, clamped);
  return clamped;
}

function ensureEnergyMeta(id) {
  let meta = energyMeta.get(id);
  if (!meta) {
    const now = Date.now() / 1000;
    meta = {
      blockedUntil: null,
      lastRegenTime: now,
    };
    energyMeta.set(id, meta);
  }
  return meta;
}

function updateEnergyMetaOnChange(id, oldEnergy, newEnergy) {
  const meta = ensureEnergyMeta(id);
  const now = Date.now() / 1000;

  if (oldEnergy > 0 && newEnergy <= 0) {
    const extra =
      config.ENERGY_EMPTY_EXTRA_DELAY_MIN +
      Math.random() * (config.ENERGY_EMPTY_EXTRA_DELAY_MAX - config.ENERGY_EMPTY_EXTRA_DELAY_MIN);
    meta.blockedUntil = now + extra;
    meta.lastRegenTime = now + extra;
  }
}

function getEnergy(id) {
  if (!entityEnergy.has(id)) {
    entityEnergy.set(id, config.DEFAULT_ENERGY);
    ensureEnergyMeta(id);
    return config.DEFAULT_ENERGY;
  }
  return entityEnergy.get(id);
}

function setEnergy(id, energy) {
  const old = entityEnergy.has(id) ? entityEnergy.get(id) : config.DEFAULT_ENERGY;
  const clamped = Math.max(0, Math.min(energy, config.DEFAULT_ENERGY));
  entityEnergy.set(id, clamped);
  updateEnergyMetaOnChange(id, old, clamped);
  return clamped;
}

function removeEnergyMeta(id) {
  energyMeta.delete(id);
}

function deleteHp(id) {
  entityHp.delete(id);
}

function energyEntries() {
  return entityEnergy.entries();
}

function hpEntries() {
  return entityHp.entries();
}

module.exports = {
  getHp,
  setHp,
  getEnergy,
  setEnergy,
  ensureEnergyMeta,
  removeEnergyMeta,
  deleteHp,
  energyEntries,
  hpEntries,
};
