# Remote assist (screen share)

## Overview

- **REST**: `GET /api/remote-assist/ice`, `POST /api/remote-assist/sessions`, `POST /api/remote-assist/sessions/join`, `GET /api/remote-assist/sessions/{id}` (includes `controlApprovedAtUtc` when set), `POST /api/remote-assist/sessions/{id}/remote-control` (body `{ "enabled": true|false }` — host enables or revokes relay of viewer `control` messages), `POST /api/remote-assist/sessions/{id}/end`
- **Signaling WebSocket**: `GET /ws/remote-assist?access_token=...&sessionId=...&role=host|viewer` (JWT must include valid `sessionId` and Redis session)
- **Media**: WebRTC peer-to-peer (STUN/TURN from config)
- **Control**: Viewer sends JSON messages with `"type":"control"` over the signaling socket. The hub **drops** these until the host calls `remote-control` with `enabled: true` (approval is stored on the session and restored on reconnect). **Tyresoles desktop (Windows)** host runs `remote_assist_pointer` to inject mouse input

## Configuration (`appsettings`)

Section `RemoteAssist`:

- `SessionTimeoutMinutes` — session row TTL (default 120)
- `JoinCodeLength` — alphanumeric join code length (6–12)
- `IceServers` — array of `{ "urls": "stun:...", "username": "...", "credential": "..." }`

Default includes Google public STUN only. **Production** should add TURN (e.g. [coturn](https://github.com/coturn/coturn)) behind TLS and firewall rules for UDP/TCP as required.

## Operations

- **Rate limiting**: named policy `RemoteAssist` (fixed window, 60/min) on REST controller
- **Logs**: Web/API use standard ASP.NET logging; monitor failed WebSocket upgrades and 401/403 on `/ws/remote-assist`
- **Database**: table `RemoteAssistSessions` on Calendar / `Db_Extra` connection; created at startup if missing

## Frontend routes

- `/assist` — landing (route guard requires permission **Remote Assist** unless super user)
- `/assist/host` — share screen (browser or Tauri)
- `/assist/join` — join with code (viewer)

## Security notes

- JWT in WebSocket query can appear in logs/proxies; prefer HTTPS/WSS and short session lifetimes
- Remote control is gated by host approval and RBAC on `/assist`; ensure a menu/permission named **Remote Assist** exists for non–super users who need access
