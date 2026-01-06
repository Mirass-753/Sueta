const WebSocket = require('ws');

function createBroadcaster(wss) {
  function broadcast(obj, exceptWs) {
    const out = JSON.stringify(obj);
    wss.clients.forEach((client) => {
      if (client.readyState !== WebSocket.OPEN) return;
      if (exceptWs && client === exceptWs) return;
      try {
        client.send(out);
      } catch (e) {
        console.warn('[WS] failed to send:', e.message);
      }
    });
  }

  function sendNpcSnapshot(ws, npcs) {
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    for (const [npcId, npc] of npcs.entries()) {
      const meta = npcs.getNpcMeta ? npcs.getNpcMeta(npcId) : null;
      const payload = {
        type: 'npc_spawn',
        npcId,
        x: npc.x,
        y: npc.y,
        hp: npc.hp,
      };

      if (meta) {
        payload.state = meta.state;
        payload.targetId = meta.targetPlayerId || null;
        payload.dirX = meta.dirX || 0;
        payload.dirY = meta.dirY || 0;
        payload.moving = !!meta.moving;
      }

      try {
        ws.send(JSON.stringify(payload));
      } catch (e) {
        console.warn('[WS] failed to send npc snapshot:', e.message);
      }
    }
  }

  function sendSnapshot(ws, { players, stats, npcs, config }) {
    if (!ws || ws.readyState !== WebSocket.OPEN) return;

    for (const [id, st] of players.entries()) {
      const snapMove = {
        type: 'move',
        id,
        x: st.x,
        y: st.y,
        dirX: st.dirX,
        dirY: st.dirY,
        moving: st.moving,
        aimAngle: typeof st.aimAngle === 'number' ? st.aimAngle : 0,
        inCombat: !!st.inCombat,
      };

      try {
        ws.send(JSON.stringify(snapMove));
      } catch (e) {
        console.warn('[WS] failed to send move snapshot:', e.message);
      }
    }

    const hpEntities = Array.from(stats.hpEntries()).map(([id, hp]) => ({ id, hp }));
    if (hpEntities.length > 0) {
      const hpSnap = {
        type: 'hp_sync',
        entities: hpEntities,
      };

      try {
        ws.send(JSON.stringify(hpSnap));
      } catch (e) {
        console.warn('[WS] failed to send hp snapshot:', e.message);
      }
    }

    const energyEntities = Array.from(stats.energyEntries()).map(([id, energy]) => ({
      id,
      energy,
      maxEnergy: config.DEFAULT_ENERGY,
    }));
    if (energyEntities.length > 0) {
      const energySnap = {
        type: 'energy_sync',
        entities: energyEntities,
      };

      try {
        ws.send(JSON.stringify(energySnap));
      } catch (e) {
        console.warn('[WS] failed to send energy snapshot:', e.message);
      }
    }

    sendNpcSnapshot(ws, npcs);
  }

  return {
    broadcast,
    sendSnapshot,
    sendNpcSnapshot,
  };
}

module.exports = {
  createBroadcaster,
};
