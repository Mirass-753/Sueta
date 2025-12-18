// server.js

// ??????? WS-?????? ??? ????????, HP ? ??????? ? ????????? ???????.

const WebSocket = require('ws');

const HOST = process.env.HOST || '127.0.0.1';
const PORT = process.env.PORT ? parseInt(process.env.PORT, 10) : 3000;

const wss = new WebSocket.Server({
  host: HOST,
  port: PORT,
});

// ================== ????????? ???? ==================

// id -> { x, y, dirX, dirY, moving, aimAngle, inCombat, t }
const playerStates = new Map();

// HP
// id -> hp
const entityHp = new Map();
const DEFAULT_HP = 100;

// ??????? (???)
// id -> energy
const entityEnergy = new Map();
const DEFAULT_ENERGY = 100;

// ???. ?????? ?? ???????: id -> { blockedUntil, lastRegenTime }
const energyMeta = new Map();

// ================== ????????? ?????? ==================

const MAX_SPEED = 20; // ????-??? ?? ????????

// ========= ????????? ?????? ??????? ==========
//
// ?? ??????:
//  - 910 ???????, ?????? ??????????????  50 ??????
//  - 1 ??????? ?????? 5 ??????
//  - ???? ??????? ????? ?? 0, ????? ??????? ?????? ????? 530 ??????
//
const ENERGY_SEGMENTS = 10;                         // ??????? "???????"
const ENERGY_SEGMENT_VALUE = DEFAULT_ENERGY / ENERGY_SEGMENTS; // 10, ???? 100/10
const ENERGY_SEGMENT_PERIOD = 5;                    // ??? ?? ???? ???????
const ENERGY_EMPTY_EXTRA_DELAY_MIN = 5;             // ???
const ENERGY_EMPTY_EXTRA_DELAY_MAX = 30;            // ???
const ENERGY_REGEN_TICK = 1;                        // ?????? ?????????? ???? ?????? (???)

// ================== ??????????????? ==================

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

// --- ??????? ---

function ensureEnergyMeta(id) {
  let meta = energyMeta.get(id);
  if (!meta) {
    const now = Date.now() / 1000;
    meta = {
      blockedUntil: null,   // ?????, ?? ???????? ????? ???????? (????? 0)
      lastRegenTime: now,   // ????? ????????? ??? ????????? ???????
    };
    energyMeta.set(id, meta);
  }
  return meta;
}

function getEnergy(id) {
  if (!entityEnergy.has(id)) {
    entityEnergy.set(id, DEFAULT_ENERGY);
    ensureEnergyMeta(id);
    return DEFAULT_ENERGY;
  }
  return entityEnergy.get(id);
}

function updateEnergyMetaOnChange(id, oldEnergy, newEnergy) {
  const meta = ensureEnergyMeta(id);
  const now = Date.now() / 1000;

  // ?????? ??? "????? ? ????"  ?????? ???. ????? ??????
  if (oldEnergy > 0 && newEnergy <= 0) {
    const extra =
      ENERGY_EMPTY_EXTRA_DELAY_MIN +
      Math.random() * (ENERGY_EMPTY_EXTRA_DELAY_MAX - ENERGY_EMPTY_EXTRA_DELAY_MIN);
    meta.blockedUntil = now + extra;
    meta.lastRegenTime = now + extra;
    // console.log(`[ENERGY] ${id} dropped to 0, regen blocked for ${extra.toFixed(1)}s`);
  }
}

function setEnergy(id, energy) {
  const old = entityEnergy.has(id) ? entityEnergy.get(id) : DEFAULT_ENERGY;
  const clamped = Math.max(0, Math.min(energy, DEFAULT_ENERGY));
  entityEnergy.set(id, clamped);
  updateEnergyMetaOnChange(id, old, clamped);
  return clamped;
}

// ????????????????? ????????
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

// ?????? ????????? ??? ?????? ???????
function sendSnapshot(ws) {
  if (!ws || ws.readyState !== WebSocket.OPEN) return;

  // ????????
  for (const [id, st] of playerStates.entries()) {
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

  // HP
  const hpEntities = Array.from(entityHp.entries()).map(([id, hp]) => ({ id, hp }));
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

  // ???????
  const energyEntities = Array.from(entityEnergy.entries()).map(([id, energy]) => ({
    id,
    energy,
    maxEnergy: DEFAULT_ENERGY,
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
}

// ================== ????????? ????????? ==================

// ????????
function handleMove(ws, msg) {
  if (typeof msg.id !== 'string') return;
  if (typeof msg.x !== 'number' || typeof msg.y !== 'number') return;

  const now = Date.now() / 1000;
  const prev = playerStates.get(msg.id);

  let x = msg.x;
  let y = msg.y;

  if (prev) {
    const dt = Math.max(now - prev.t, 1 / 60);
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
    }
  }

  ws.playerId = ws.playerId || msg.id;

  // ?????????????? HP ? ???????
  getHp(msg.id);
  getEnergy(msg.id);

  playerStates.set(msg.id, {
    x,
    y,
    dirX: typeof msg.dirX === 'number' ? msg.dirX : 0,
    dirY: typeof msg.dirY === 'number' ? msg.dirY : 0,
    moving: !!msg.moving,
    aimAngle: typeof msg.aimAngle === 'number' ? msg.aimAngle : 0,
    inCombat: !!msg.inCombat,
    t: now,
  });

  const out = {
    type: 'move',
    id: msg.id,
    x,
    y,
    dirX: typeof msg.dirX === 'number' ? msg.dirX : 0,
    dirY: typeof msg.dirY === 'number' ? msg.dirY : 0,
    moving: !!msg.moving,
    aimAngle: typeof msg.aimAngle === 'number' ? msg.aimAngle : 0,
    inCombat: !!msg.inCombat,
  };

  broadcast(out, ws);
}

// ???? ?? HP
function handleDamageRequest(ws, msg) {
  const sourceId = typeof msg.sourceId === 'string' ? msg.sourceId : null;
  const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
  const amount = typeof msg.amount === 'number' ? msg.amount : 0;

  if (!targetId || amount <= 0) return;

  const oldHp = getHp(targetId);
  const newHp = setHp(targetId, oldHp - amount);
  const appliedDamage = Math.max(0, oldHp - newHp);

  const evt = {
    type: 'damage',
    sourceId,
    targetId,
    amount,
    hp: newHp,
  };

  console.log('[WS] damage', evt);

  if (appliedDamage > 0) {
    const targetState = playerStates.get(targetId) || {};

    const popupX = typeof msg.x === 'number'
      ? msg.x
      : typeof targetState.x === 'number'
        ? targetState.x
        : 0;
    const popupY = typeof msg.y === 'number'
      ? msg.y
      : typeof targetState.y === 'number'
        ? targetState.y
        : 0;
    const popupZ = typeof msg.z === 'number'
      ? msg.z
      : 0;

    const popupMsg = {
      type: 'damage_popup',
      amount: Math.round(appliedDamage),
      x: popupX,
      y: popupY,
      z: popupZ,
    };
    broadcast(popupMsg);
  }

  broadcast(evt);
}

// ???? ?? ??????? (???)
function handleEnergyRequest(ws, msg) {
  const targetId = typeof msg.targetId === 'string' ? msg.targetId : null;
  const amount = typeof msg.amount === 'number' ? msg.amount : 0;

  if (!targetId || amount <= 0) return;

  const oldEnergy = getEnergy(targetId);
  const newEnergy = setEnergy(targetId, oldEnergy - amount);

  const evt = {
    type: 'energy_update',
    targetId,
    energy: newEnergy,
    maxEnergy: DEFAULT_ENERGY,
  };

  console.log('[WS] energy_update', evt);

  broadcast(evt);
}

// ??????????
function handleDisconnect(ws) {
  const id = ws.playerId;
  if (!id) return;

  playerStates.delete(id);
  // HP / Energy ????? ?? ???????, ???? ??????,
  // ????? ??? reconnection ??? ???????????

  energyMeta.delete(id);

  const msg = {
    type: 'disconnect',
    id,
  };

  broadcast(msg);
}

// ================== ÐÐ ÐÐÐÐÐ¢Ð« ==================
function handleItemDrop(ws, msg) {
  if (!msg || typeof msg.pickupId !== 'string' || typeof msg.itemName !== 'string') return;

  const evt = {
    type: 'item_drop',
    pickupId: msg.pickupId,
    itemName: msg.itemName,
    x: typeof msg.x === 'number' ? msg.x : 0,
    y: typeof msg.y === 'number' ? msg.y : 0,
  };

  broadcast(evt, ws);
}

function handleItemPickup(ws, msg) {
  if (!msg || typeof msg.pickupId !== 'string') return;

  const evt = {
    type: 'item_pickup',
    pickupId: msg.pickupId,
  };

  // ÐÐ¾Ð¾ÑÐ´Ð¸Ð½Ð°ÑÑ Ð¸ itemName Ð¼Ð¾Ð³ÑÑ Ð¿ÑÐ¸Ð³Ð¾Ð´Ð¸ÑÑÑÑ Ð´Ð»Ñ Ð±ÑÐ´ÑÑÐµÐ¹ Ð»Ð¾Ð³Ð¸ÐºÐ¸,
  // Ð¿Ð¾ÑÑÐ¾Ð¼Ñ Ð¾ÑÑÐ°Ð²Ð»ÑÐµÐ¼ Ð¸Ñ, ÐµÑÐ»Ð¸ ÐºÐ»Ð¸ÐµÐ½Ñ Ð¿ÑÐ¸ÑÐ»Ð°Ð».
  if (typeof msg.itemName === 'string') evt.itemName = msg.itemName;
  if (typeof msg.x === 'number') evt.x = msg.x;
  if (typeof msg.y === 'number') evt.y = msg.y;

  broadcast(evt, ws);
}

// ================== ????????? ????? ??????? ==================
//
// ???????, ? ???? energy < max ? ??? ????? ????? ?????????,
// ??? ? 5 ?????? ????????? ?? ?????? ???????.
//
setInterval(() => {
  const now = Date.now() / 1000;

  for (const [id, currentEnergy] of entityEnergy.entries()) {
    if (currentEnergy >= DEFAULT_ENERGY) continue;

    const meta = ensureEnergyMeta(id);

    // ??? ???? ???. ????? ????? "????"
    if (meta.blockedUntil && now < meta.blockedUntil) continue;

    // ??? ?? ?????? 5 ?????? ? ?????????? ???????
    if (now - meta.lastRegenTime < ENERGY_SEGMENT_PERIOD) continue;

    const oldEnergy = currentEnergy;
    const newEnergy = setEnergy(id, oldEnergy + ENERGY_SEGMENT_VALUE);
    meta.lastRegenTime = now;

    if (newEnergy !== oldEnergy) {
      const evt = {
        type: 'energy_update',
        targetId: id,
        energy: newEnergy,
        maxEnergy: DEFAULT_ENERGY,
      };
      broadcast(evt);
    }
  }
}, ENERGY_REGEN_TICK * 1000);

// ================== WS-?????? ==================

wss.on('connection', (ws) => {
  console.log('[WS] client connected');
  ws.playerId = null;

  sendSnapshot(ws);

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
      case 'energy_request':
        handleEnergyRequest(ws, msg);
        break;
      case 'item_drop':
        handleItemDrop(ws, msg);
        break;
      case 'item_pickup':
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
