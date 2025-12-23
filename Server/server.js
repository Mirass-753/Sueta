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

wss.on('connection', (ws) => {
  console.log('[WS] client connected');

  // Отправляем текущий снимок состояния подключившемуся клиенту
  broadcaster.sendSnapshot(ws, { players, stats, npcs, config });

  ws.on('message', (data) => {
    handlers.onMessage(ws, data);
  });

  ws.on('close', () => {
    console.log('[WS] connection closed');
    handlers.onDisconnect(ws);
  });

  ws.on('error', (err) => {
    console.warn('[WS] connection error', err);
  });
});

wss.on('error', (err) => {
  console.error('[WS] server error', err);
});

wss.on('close', () => {
  console.log('[WS] server closed');
});

wss.on('listening', () => {
  console.log(`[WS] Server listening on ws://${config.HOST}:${config.PORT}${config.WS_PATH}`);
});
