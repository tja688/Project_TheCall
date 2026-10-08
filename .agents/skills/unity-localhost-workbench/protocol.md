# Workbench protocol

## Launch

- Bind `HttpListener` to `http://127.0.0.1:{port}/` and `http://localhost:{port}/` only
- Prefer port from `SessionState`; else first free in a small reserved range
- Generate a 32-byte token once per Editor session; store token+port in `SessionState`
- `LaunchUrl` = `http://127.0.0.1:{port}/#token=<token>`
- On quit / before assembly reload: stop listener; authoring may flush transient first

## Static routes (no token)

| Path | Body |
|------|------|
| `GET /` or `/index.html` | HTML |
| `GET /styles.css` | CSS |
| `GET /app.js` | JS |

Headers: strict CSP (`default-src 'self'; connect-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'`), `X-Content-Type-Options: nosniff`, `Cache-Control: no-store`.

Reject any path containing `..` or `\`.

## API routes (Bearer required)

| Path | Method | Role |
|------|--------|------|
| `/api/snapshot` | GET | Full envelope at current revision |
| `/api/events?afterRevision=&timeoutMs=` | GET | Long-poll; return on newer revision or timeout (default ~25s) |
| `/api/command` | POST | `{ requestId, command, payload }` → `commandResult` |

`Authorization: Bearer <token>`. Cap body size (default 1 MiB).

## Stream

`GET /stream` WebSocket upgrade. Within 5s client sends:

```json
{ "type":"hello", "token":"...", "afterRevision":0 }
```

Then server pushes `snapshot` / `delta` / `commandResult`. Commands may also arrive on the socket as the same POST body shape.

If `AcceptWebSocketAsync` is unavailable, disable `/stream` and use `/api/events` only. Same envelopes either way.

## Envelopes

```json
{ "type":"snapshot", "protocolVersion":1, "revision":42, "payload":{ } }
{ "type":"delta", "protocolVersion":1, "revision":43, "payload":{ } }
{ "type":"commandResult", "requestId":"r1", "ok":true, "revision":43, "payload":{ }, "error":"" }
```

Rules:

- `protocolVersion` is fixed for the workbench; mismatch → client must stop or hard-refresh
- After connect / domain reload: always take a full `snapshot` and replace client state
- Accept `delta` only when `revision === currentRevision + 1`; else request another snapshot
- Emit nothing when fingerprints are unchanged (revision, latest event id, session dirty hash, …)
- Serialize with a real JSON library (dictionaries/lists); avoid `JsonUtility` for envelopes

## Main-thread dispatch

Accept loop runs on a thread-pool task. Every Unity/Editor/session touch goes through a `ConcurrentQueue` pumped on `EditorApplication.update` (cap jobs per tick). Broadcast at most one state push per tick unless a command result must flush immediately.

## Security checklist

- [ ] Non-loopback `RemoteEndPoint` → 403
- [ ] Host/Origin not exactly `http://127.0.0.1:{port}` or `http://localhost:{port}` → 403
- [ ] Missing/wrong Bearer or WS hello token → 401
- [ ] Path traversal → 400
- [ ] Oversize body → 413
- [ ] Unknown command / invalid enum / non-finite numbers → rejected with `commandResult.ok=false`
- [ ] Static and API never bind non-loopback prefixes

## Minimal command set

Every workbench needs at least:

- One read path: snapshot (GET or push)
- Domain commands as an allow-list (no free-form script eval over the wire)

Authoring workbenches typically also expose: replace-row, save, saveAll, revert, revertAll, reloadFromDisk, recoverTransient, discardTransient, plus domain preview/stop helpers.
