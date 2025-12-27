function startEnergyRegen({ stats, config, broadcast, npcs, players }) {
  setInterval(() => {
    const now = Date.now() / 1000;

    npcs.spawnNpcsIfNeeded({
      broadcast,
      setHp: stats.setHp,
      defaultHp: config.DEFAULT_HP,
      spawnPoints: config.NPC_SPAWN_POINTS,
      players,
      config,
    });

    for (const [id, currentEnergy] of stats.energyEntries()) {
      if (currentEnergy >= config.DEFAULT_ENERGY) continue;

      const meta = stats.ensureEnergyMeta(id);

      if (meta.blockedUntil && now < meta.blockedUntil) continue;
      if (now - meta.lastRegenTime < config.ENERGY_SEGMENT_PERIOD) continue;

      const oldEnergy = currentEnergy;
      const newEnergy = stats.setEnergy(id, oldEnergy + config.ENERGY_SEGMENT_VALUE);
      meta.lastRegenTime = now;

      if (newEnergy !== oldEnergy) {
        const evt = {
          type: 'energy_update',
          targetId: id,
          energy: newEnergy,
          maxEnergy: config.DEFAULT_ENERGY,
        };
        broadcast(evt);
      }
    }
  }, config.ENERGY_REGEN_TICK * 1000);
}

module.exports = {
  startEnergyRegen,
};
