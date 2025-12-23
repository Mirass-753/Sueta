const WebSocket = require('ws');
const config = require('./config/constants');
const players = require('./state/players');
const npcs = require('./state/npcs');
const stats = require('./state/stats');
const { createBroadcaster } = require('./net/broadcast');
const { createHandlers } = require('./net/handlers');
const { startEnergyRegen } = require('./systems/energyRegenSystem');
const { startNpcBroadcast } = require('./systems/npcBroadcastSystem');
const { startNpcAiLoop } = require('./systems/npcAiSystem');

const wss = new WebSocket.Server({
  host: config.HOST,
  port: config.PORT,
  path: config.WS_PATH,
});

const broadcaster = createBroadcaster(wss);
const handlers = createHandlers({
  players,
  npcs,
  stats,
  config,
  broadcast: broadcaster.broadcast,
});
startEnergyRegen({ stats, config, broadcast: broadcaster.broadcast, npcs });
startNpcBroadcast({ npcs, config, broadcast: broadcaster.broadcast });
startNpcAiLoop({ npcs, config });

  broadcaster.sendSnapshot(ws, { players, stats, npcs, config });

    handlers.onMessage(ws, data);
    handlers.onDisconnect(ws);
  console.log(`[WS] Server listening on ws://${config.HOST}:${config.PORT}`);
        handleItemPickup(ws, msg);
        break;
      default:
        break;
    }
  });

  ws.on('close', () => {
    console.log('[WS] connection closed');
    handleDisconnect(ws);
  });
});

wss.on('listening', () => {
  console.log(`[WS] Server listening on ws://${HOST}:${PORT}`);
});
