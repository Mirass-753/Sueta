# CatLaw WebSocket Server

This server keeps authoritative movement and HP state for the Unity clients.
Use it together with the Unity project in this repo to test server-side damage
flow.

## Runtime configuration
- `HOST` (default `0.0.0.0`)
- `PORT` (default `3000`)
- `WS_PATH` (default `/game-ws`) — must match the Unity client endpoint.

## Setup
1. Install Node.js 18+.
2. Install dependencies:
   ```bash
   npm install
   ```
3. Start the server locally (defaults to port 3000):
   ```bash
   node server.js
   ```
   The server listens on `ws://0.0.0.0:3000/game-ws` by default.

To expose it publicly, place it behind a reverse proxy (e.g., nginx) that maps
`/ws` to this port. Update the Unity client endpoint accordingly if you are not
using `wss://catlaw.online/ws`.

## Local smoke test
1. Start the server:
   ```bash
   node server.js
   ```
   You should see: `Server listening on ws://0.0.0.0:3000/game-ws`.
2. Connect with `wscat` (install via `npm i -g wscat`):
   ```bash
   wscat -c ws://127.0.0.1:3000/game-ws
   ```
   On connect the server logs the request `url` and remote address and will emit periodic ping/pong entries.

## Message flow
- Clients send `move` and `damage_request` messages.
- The server stores `playerStates` and `entityHp` by `id` / `targetId`.
- On `damage_request`, the server subtracts HP and broadcasts a `damage` event
  to **all** clients with `{ targetId, hp }`.
- Clients apply HP changes only via `HealthSystem.SetCurrentHpFromServer` after
  resolving `targetId` to a `Damageable` component.

## Quick test steps
1. Run the server locally.
2. Start two game clients that connect to this server.
3. Move both clients so they see each other.
4. Attack from client A:
   - A should log `[NET] Send damage_request...`.
   - Server logs `[WS] damage { sourceId, targetId, amount, newHp }`.
   - Both clients log `[NET] Damage event: targetId=..., hp=..., found=true` and
     update the target's HP identically.

If any step fails, ensure the `networkId` set in `PlayerController` is the same
ID used in movement (`NetMessageMove.id`) and damage (`targetId`).
