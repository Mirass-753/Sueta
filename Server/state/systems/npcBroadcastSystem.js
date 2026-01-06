function startNpcBroadcast({ npcs, config, broadcast }) {
  setInterval(() => {
    if (npcs.size() === 0) return;

    for (const [npcId, npc] of npcs.entries()) {
      const meta = npcs.getNpcMeta ? npcs.getNpcMeta(npcId) : null;
      const stateMsg = {
        type: 'npc_state',
        npcId,
        x: npc.x,
        y: npc.y,
        hp: npc.hp,
      };

      if (meta) {
        stateMsg.state = meta.state;
        stateMsg.targetId = meta.targetPlayerId || null;
        stateMsg.dirX = meta.dirX || 0;
        stateMsg.dirY = meta.dirY || 0;
        stateMsg.moving = !!meta.moving;
      }

      broadcast(stateMsg);
    }
  }, config.NPC_STATE_BROADCAST_PERIOD_MS);
}

module.exports = {
  startNpcBroadcast,
};
