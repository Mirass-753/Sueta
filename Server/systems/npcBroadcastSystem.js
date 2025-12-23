function startNpcBroadcast({ npcs, config, broadcast }) {
  setInterval(() => {
    if (npcs.size() === 0) return;

    for (const [npcId, npc] of npcs.entries()) {
      const stateMsg = {
        type: 'npc_state',
        npcId,
        x: npc.x,
        y: npc.y,
        hp: npc.hp,
      };

      broadcast(stateMsg);
    }
  }, config.NPC_STATE_BROADCAST_PERIOD_MS);
}

module.exports = {
  startNpcBroadcast,
};
