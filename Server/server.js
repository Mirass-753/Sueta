// Minimal WebSocket relay for CatLaw. Handles movement sync and authoritative HP.
const WebSocket = require('ws');

const PORT = process.env.PORT || 3000;
const wss = new WebSocket.Server({ port: PORT });

const playerStates = new Map(); // id -> last move state
const entityHp = new Map();     // id -> hp
const DEFAULT_HP = 100;

function getHp(id) {
  if (!entityHp.has(id)) {
    entityHp.set(id, DEFAULT_HP);
  }
  return entityHp.get(id);
}

function broadcast(obj) {
  const out = JSON.stringify(obj);
  wss.clients.forEach((client) => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(out);
    }
  });
}

function handleMove(ws, msg) {
  if (typeof msg.id !== 'string') return;

  ws.playerId = ws.playerId || msg.id;

  playerStates.set(msg.id, {
    x: msg.x,
    y: msg.y,
    dirX: msg.dirX,
    dirY: msg.dirY,
    moving: !!msg.moving,
  });

  broadcast({
    type: 'move',
    id: msg.id,
    x: msg.x,
    y: msg.y,
    dirX: msg.dirX,
    dirY: msg.dirY,
    moving: !!msg.moving,
  });
}

function handleDamageRequest(ws, msg) {
  const sourceId = typeof msg.sourceId === 'string' ? msg.sourceId : null;
  const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
  const amount = typeof msg.amount === 'number' ? msg.amount : 0;

  if (!targetId || amount <= 0) return;

  const oldHp = getHp(targetId);
  const newHp = Math.max(0, oldHp - amount);
  entityHp.set(targetId, newHp);

  const evt = {
    type: 'damage',
    sourceId,
    targetId,
    amount,
    hp: newHp,
  };

  console.log('[WS] damage', { sourceId, targetId, amount, newHp });
  broadcast(evt);
}

function handleDisconnect(id) {
  if (!id) return;
  playerStates.delete(id);
  // HP остаётся для краткосрочной сессии: можно очистить по таймеру при необходимости
  broadcast({ type: 'disconnect', id });
}

wss.on('connection', (ws) => {
  console.log('[WS] client connected');

  ws.on('message', (data) => {
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch (err) {
      console.warn('[WS] failed to parse message', err);
      return;
    }

    if (!msg || typeof msg.type !== 'string') return;

    switch (msg.type) {
      case 'move':
        handleMove(ws, msg);
        break;
      case 'damage_request':
        handleDamageRequest(ws, msg);
        break;
      default:
        break;
    }
  });

  ws.on('close', () => {
    if (ws.playerId) {
      console.log('[WS] client closed', ws.playerId);
      handleDisconnect(ws.playerId);
    }
  });
});

wss.on('listening', () => {
  console.log('[WS] listening on port', PORT);
});
