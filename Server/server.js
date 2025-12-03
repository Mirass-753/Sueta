const WebSocket = require('ws');

// Хост/порт: по умолчанию слушаем только 127.0.0.1,
// наружу выходим через nginx (wss://.../ws).
const HOST = process.env.HOST || '127.0.0.1';
const PORT = process.env.PORT ? parseInt(process.env.PORT, 10) : 3000;

const wss = new WebSocket.Server({
  host: HOST,
  port: PORT,
});

// ---- Состояния игроков ----
// id -> { x, y, dirX, dirY, moving, t }
const playerStates = new Map();

// ---- HP сущностей (игроки / другие объекты) ----
// id -> hp
const entityHp = new Map();
const DEFAULT_HP = 100;

// Ограничение скорости (юнитов/сек). Кот бегает где-то 5–10, 20 — с запасом.
const MAX_SPEED = 20;

// Получение / ленивое создание HP сущности.
function getHp(id) {
  if (!entityHp.has(id)) {
    entityHp.set(id, DEFAULT_HP);
  }
  return entityHp.get(id);
}

function setHp(id, hp) {
  const clamped = Math.max(0, hp);
  entityHp.set(id, clamped);
  return clamped;
}

// Общая рассылка сообщения всем клиентам.
// exceptWs — сокет, которому НЕ надо отправлять (например, отправитель движения).
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

// Отправка снапшота состояний (позиции + HP) новому клиенту.
function sendSnapshot(ws) {
  if (!ws || ws.readyState !== WebSocket.OPEN) return;

  // Сначала — все позиции
  for (const [id, st] of playerStates.entries()) {
    const snapMove = {
      type: 'move',
      id: id,
      x: st.x,
      y: st.y,
      dirX: st.dirX,
      dirY: st.dirY,
      moving: st.moving,
    };

    try {
      ws.send(JSON.stringify(snapMove));
    } catch (e) {
      console.warn('[WS] failed to send move snapshot:', e.message);
    }
  }

  // Затем — HP всех сущностей
  const entities = Array.from(entityHp.entries()).map(([id, hp]) => ({ id, hp }));
  if (entities.length > 0) {
    const hpSnap = {
      type: 'hp_sync',
      entities,
    };

    try {
      ws.send(JSON.stringify(hpSnap));
    } catch (e) {
      console.warn('[WS] failed to send hp snapshot:', e.message);
    }
  }
}

// ------------ Обработка движения ------------

function handleMove(ws, msg) {
  if (typeof msg.id !== 'string') return;
  if (typeof msg.x !== 'number' || typeof msg.y !== 'number') return;

  const now = Date.now() / 1000; // секунды
  const prev = playerStates.get(msg.id);

  let x = msg.x;
  let y = msg.y;

  if (prev) {
    const dt = Math.max(now - prev.t, 1 / 60); // защита от dt=0
    const dx = x - prev.x;
    const dy = y - prev.y;
    const dist = Math.sqrt(dx * dx + dy * dy);
    const speed = dist / dt;

    if (speed > MAX_SPEED) {
      const maxDist = MAX_SPEED * dt;

      if (dist > 0) {
        const scale = maxDist / dist;
        x = prev.x + dx * scale;
        y = prev.y + dy * scale;
      } else {
        x = prev.x;
        y = prev.y;
      }

      // Для дебага можно раскомментить:
      // console.log(`[WS] Clamped speed for ${msg.id}: ${speed.toFixed(2)} -> ${MAX_SPEED}`);
    }
  }

  ws.playerId = ws.playerId || msg.id;

  // ВАЖНО: гарантируем, что у сущности есть запись HP
  // (это как раз то, что добавлял Codex).
  getHp(msg.id);

  playerStates.set(msg.id, {
    x: x,
    y: y,
    dirX: typeof msg.dirX === 'number' ? msg.dirX : 0,
    dirY: typeof msg.dirY === 'number' ? msg.dirY : 0,
    moving: !!msg.moving,
    t: now,
  });

  const out = {
    type: 'move',
    id: msg.id,
    x: x,
    y: y,
    dirX: typeof msg.dirX === 'number' ? msg.dirX : 0,
    dirY: typeof msg.dirY === 'number' ? msg.dirY : 0,
    moving: !!msg.moving,
  };

  // Отправляем всем, КРОМЕ отправителя (он двигает себя локально).
  broadcast(out, ws);
}

// ------------ Обработка серверного урона ------------

function handleDamageRequest(ws, msg) {
  const sourceId = typeof msg.sourceId === 'string' ? msg.sourceId : null;
  const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
  const amount   = typeof msg.amount   === 'number' ? msg.amount   : 0;

  if (!targetId || amount <= 0) return;

  const oldHp = getHp(targetId);
  const newHp = setHp(targetId, oldHp - amount);

  const evt = {
    type: 'damage',
    sourceId: sourceId,
    targetId: targetId,
    amount: amount,
    hp: newHp,
  };

  console.log('[WS] damage', evt);

  // HP-событие отправляем всем.
  broadcast(evt);
}

// ------------ Отключение клиента ------------

function handleDisconnect(ws) {
  const id = ws.playerId;
  if (!id) return;

  playerStates.delete(id);
  // HP можно не удалять — пригодится, если игрок переподключится.

  const msg = {
    type: 'disconnect',
    id: id,
  };

  broadcast(msg);
}

// ------------ Подключения ------------

wss.on('connection', (ws) => {
  console.log('[WS] client connected');
  ws.playerId = null;

  // Сразу отправляем снапшот состояний новому клиенту.
  sendSnapshot(ws);

  // отправляем текущие HP всех сущностей, чтобы клиент видел актуальное состояние
  sendHpSnapshot(ws);

  ws.on('message', (data) => {
    let msg;
    try {
      msg = JSON.parse(data.toString());
    } catch (err) {
      console.warn('[WS] failed to parse message', err);
      return;
    }

    if (!msg || typeof msg !== 'object') return;

    const type = typeof msg.type === 'string' ? msg.type : 'move';

    switch (type) {
      case 'move':
        handleMove(ws, msg);
        break;
      case 'damage_request':
        handleDamageRequest(ws, msg);
        break;
      default:
        // Другие типы пока игнорируем
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
