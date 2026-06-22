# Server Platform implementation status

Last source audit: 21 June 2026

This document describes what is present in the repository today. It is based on
the source code rather than release notes or the intended roadmap. “Partial” means
that usable code exists, but the feature is incomplete, disconnected from the UI,
or has known correctness/security limitations.

## Status summary

| Area | Status | Current implementation |
| --- | --- | --- |
| Backend API | Implemented | Windows `HttpListener` backend with JSON, file-transfer, and SSE endpoints. |
| Local authentication | Partial | Registration, login, JWT access tokens, rotating refresh tokens, logout, and persistent revocation are implemented. Roles, recovery, rate limiting, and several hardening items are not. |
| Server creation | Partial | Paper, Vanilla, Velocity, and locally imported Spigot/Bukkit JARs have creation paths. Purpur and BungeeCord are empty stubs; Fabric is not implemented. |
| Server lifecycle | Implemented | Per-owner start, stop, restart, process tracking, graceful stop with forced termination fallback, and command input. |
| Server console | Implemented | Bounded output for the current process run and live Server-Sent Events are available through an authenticated web console. History is cleared between server runs. |
| Metrics | Implemented | Per-process CPU and memory sampling is displayed by the web dashboard for running server instances. |
| File management | Partial | The panel exposes all implemented file operations, including routes with documented ownership gaps. See `DEVNOTES.md`. |
| User profiles | Partial | Public profile lookup and an owner-scoped server list exist. Profile editing and account management do not. |
| Java runtime management | Partial | Temurin, Zulu, and Liberica download paths and a runtime index exist. Corretto is declared but has no download implementation. |
| Web panel | Partial | Renewable authentication, server-card navigation/management/metrics, live console, and all implemented file operations are connected to the backend. Public profile lookup is intentionally not exposed. |
| Windows desktop manager | Prototype | Local HTML login and basic profile display exist. It does not expose the server-management features. |
| Installer | Partial | A Windows Forms installer project exists, but this audit does not treat packaging as production-verified. |
| Plugin/mod manager | Not implemented | No plugin or mod discovery, install, update, removal, or API routes exist. |
| Scheduler/automation | Not implemented | No jobs, schedules, task persistence, or execution engine exist. |
| Roles and administrative permissions | Not implemented | Tokens identify a username only. There are no role claims, role models, or administrator-only policies. |

## Implemented backend features

### Authentication and sessions

- Local users persisted in `users.json`.
- Passwords stored using PBKDF2-SHA256 with a per-password random salt and
  100,000 iterations.
- Successful login migration from the legacy unsalted SHA-256 password format.
- One-hour signed JWT access tokens with issuer, audience, lifetime, algorithm,
  and token-ID validation.
- Thirty-day opaque refresh tokens. Only their SHA-256 hashes are persisted.
- Refresh-token rotation and token-family revocation when a rotated token is
  replayed.
- Persistent refresh sessions and revoked access-token IDs in
  `auth_sessions.json`.
- Logout revocation for the supplied access token and, when provided, the
  refresh-token family.
- Automatic replacement of the publicly known legacy JWT signing secret.
- Generic login errors to avoid revealing whether an account exists.

The login response retains `token` as a backwards-compatible alias for
`accessToken`.

### Minecraft server management

- Owner-scoped server index persisted in `servers.json`.
- Cryptographically random 128-bit hexadecimal server IDs.
- Server directory and configuration generation.
- Server JAR retrieval for Paper, Vanilla, and Velocity.
- Local Spigot/Bukkit JAR repository and a separate console import utility.
- Java process launch with configured minimum/maximum memory.
- Asynchronous stdout/stderr capture with a bounded in-memory log.
- Graceful stop command followed by forced process termination after timeout.
- Live console output over SSE with heartbeat messages.
- Console command forwarding to the running Java process.
- Per-process CPU and working-set memory metrics.
- Owner checks on creation, deletion, lifecycle control, metrics, console
  streaming, command input, file listing, and text read/write/delete.

### Files and platform services

- File listing, recursive listing, text read/write, deletion, directory creation,
  move/rename, multipart upload, and binary download.
- Canonical-path checking intended to keep file operations inside a server's
  `files` directory.
- Runtime download/index infrastructure for multiple Java distributions.
- First-run configuration, configurable API port/server directory, URL ACL
  setup, single-instance enforcement, connectivity checks, and file logging.

## HTTP API

### Conventions

- Default base URL: `http://localhost:5678`.
- Protected endpoints expect `Authorization: Bearer <accessToken>`.
- JSON requests should send `Content-Type: application/json` unless noted.
- Success and error shapes are not yet fully standardized. Most mutation routes
  return a `success` boolean, while `GET /profile/servers` returns an array.
- There is no API version prefix, OpenAPI description, or generated client.

### Authentication

#### `POST /auth/login`

Body:

```json
{ "username": "admin", "password": "admin" }
```

Successful response:

```json
{
  "success": true,
  "token": "<access-token>",
  "accessToken": "<access-token>",
  "refreshToken": "<refresh-token>",
  "tokenType": "Bearer",
  "expiresIn": 3600,
  "refreshExpiresIn": 2592000
}
```

#### `POST /auth/refresh`

Rotates the refresh token. The previous token must be discarded immediately.
Reusing it revokes the entire token family.

```json
{ "refreshToken": "<refresh-token>" }
```

The successful response has the same token shape as login.

#### `POST /auth/logout`

Requires the access-token header. Include the current refresh token to revoke
the complete refresh-token family rather than only the access token.

```json
{ "refreshToken": "<refresh-token>" }
```

#### `POST /auth/register`

```json
{ "username": "new-user", "password": "password" }
```

Registration is currently public and creates an ordinary account. There is no
role assignment or administrator approval workflow.

### Profiles and discovery

| Method and path | Auth | Input | Result |
| --- | --- | --- | --- |
| `GET /endpointinfo` | No | None | Backend name and hard-coded version. |
| `POST /profile/public` | No | JSON: `username` | Public user fields with `PasswordHash` removed. |
| `GET /profile/servers` | Yes | None | Array of servers owned by the authenticated username. |

### Server lifecycle and metrics

All routes below require a bearer access token and verify that the authenticated
user owns the requested server.

| Method and path | Input | Result/notes |
| --- | --- | --- |
| `POST /servers/create` | Creation object described below | Creates server files and returns the generated `id`. |
| `POST /servers/delete` | JSON: `id` | Deletes the index entry and server directory recursively. |
| `POST /servers/start` | JSON: `id` | Starts the configured Java process. |
| `POST /servers/stop` | JSON: `id` | Queues a graceful stop operation. |
| `POST /servers/restart` | JSON: `id` | Queues stop followed by start. |
| `POST /servers/metrics` | JSON: `id` | Returns `running`, `cpu`, `memory`, and `memoryMB`. The server must have a tracked instance. |
| `POST /servers/console/command` | JSON: `id`, `command` | Sends a line to the server process. The current handler does not send a normal success response and should be considered incomplete. |
| `GET /servers/console/stream?id=...` | Query: `id` | `text/event-stream` output plus five-second heartbeats. |

Server creation body:

```json
{
  "serverName": "My Server",
  "serverDesc": "Description",
  "software": "paper",
  "version": "1.21.5/123",
  "minRam": "1024",
  "maxRam": "4096",
  "javaVer": "21",
  "javaVendor": "Temurin",
  "javaType": "jre"
}
```

`version` is slash-delimited. Vanilla expects only a Minecraft version; Paper
and Velocity expect a version and build. Spigot/Bukkit use a locally imported
JAR. Inputs are not yet formally validated, and the creation implementation can
report success even after some internal creation failures.

### Server files

| Method and path | Input | Result/notes |
| --- | --- | --- |
| `GET /servers/files/list` | Query: `id`, optional `path`, optional `recursive=true` | Returns file metadata entries. Owner check present. |
| `GET /servers/files/read` | Query: `id`, `path` | Returns a text file as JSON. Owner check present. |
| `POST /servers/files/write` | JSON: `id`, `path`, `content` | Creates/replaces a text file. Owner check present. |
| `POST /servers/files/delete` | JSON: `id`, `path` | Deletes a file or directory recursively. Owner check present. |
| `POST /servers/files/mkdir` | JSON: `id`, `path` | Creates a directory. **Owner check missing.** |
| `POST /servers/files/move` | JSON: `id`, `from`, `to`, optional `overwrite` | Moves/renames an entry. **Owner check missing.** |
| `GET /servers/files/download` | Query: `id`, `path` | Binary download. **Owner check missing.** |
| `POST /servers/files/upload` | `multipart/form-data`: `id`, `path`, optional `overwrite`, one file | Uploads a file. **Owner check missing.** |

All file handlers require a valid bearer token, but the four routes explicitly
marked above currently allow any authenticated user to operate on another
user's server if its ID is known. They must not be exposed to untrusted users
until owner checks are added.

## README feature promises compared with the repository

### Minecraft Server Management — partial

Start, stop, restart, creation, deletion, metrics, files, and console primitives
exist in the backend. The web panel does not yet expose a working management
experience. Purpur, BungeeCord, and Fabric creation are absent or stubbed.

### Plugin/Mod Manager — not implemented

The README promises installation and quick updates for plugins and mods. There
are no backend models, catalog integrations, API routes, update checks, or UI
screens for this feature.

### Web-Based Admin Panel — partial

The Blazor WebAssembly application connects login, registration, logout,
refresh-token rotation, protected management routes, server-card navigation and
creation, lifecycle controls, metrics, console streaming/commands, and file
operations to the backend. Public profile lookup is intentionally left out of
the panel. Remaining limitations include:

- the backend URL defaults to `http://localhost:5678/` and must be configured
  in `wwwroot/appsettings.json` for remote deployments;
- there is no role/account-management experience because those backend systems
  do not exist;
- file upload/download/mkdir/move are exposed for development despite missing
  owner checks and are documented in `DEVNOTES.md`;
- there are no browser integration tests or offline/PWA session behaviors.

### Secure Auth with Roles (Admin/User) — partial auth, roles absent

Local password authentication and renewable/revocable tokens are implemented.
There is no role field, role claim, authorization policy, administrator-only
endpoint, or distinction between admin and ordinary user permissions.

### Server Logs and Console Output — backend implemented

Process output capture and buffered/live logs are available through an
authenticated panel screen. Commands are exposed with a short client timeout
because the backend command handler still omits a normal success response.

### File Manager — backend partial

The panel can browse, read, create, edit, delete, upload, download, create
directories, and move/rename entries. Four exposed handlers are missing
server-owner authorization checks. There are no backend file size, upload size,
quota, or text-file size limits; the browser imposes a 64 MB upload cap.

### Scheduler & Task Automation — not implemented

There is no scheduler, task model, persisted job queue, recurring action engine,
backup schedule, or related API/UI.

### Web API for integrations — partial

A substantial HTTP API exists, but it is not yet a stable integration API. It
lacks versioning, formal schemas, consistent status/error responses, OpenAPI,
API keys/service accounts, pagination, and compatibility guarantees.

### Modern UI — partial

The responsive Blazor/MudBlazor panel provides the implemented authentication,
server, metrics, console, and safe file-management workflows. Features without
backend support remain absent, so the promised panel is not yet feature-complete.

## Known gaps and risks

These are the highest-priority issues observed during the audit:

1. Add owner checks to file upload, download, mkdir, and move operations.
2. Make safe-path validation separator-aware; a plain string prefix check can
   accept a sibling path whose name begins with the expected root name.
3. Replace the default `admin` / `admin` onboarding credentials with a secure
   first-run setup.
4. Restrict or disable public registration for installations that are not meant
   to be multi-user public services.
5. Add role-based authorization before describing accounts as Admin/User.
6. Put the backend behind HTTPS; credentials and bearer tokens are otherwise
   exposed in transit.
7. Replace permissive reflected CORS with an explicit trusted-origin allowlist.
8. Add login rate limiting, password policy, password change/recovery, and
   “revoke all sessions” support.
9. Connect the web client to refresh-token rotation and avoid long-lived tokens
   in broadly accessible browser storage where possible.
10. Make server creation transactional so failed downloads/configuration do not
   leave index entries or return false success.
11. Standardize request validation, response bodies, HTTP status codes, and
    exception handling across endpoints.
12. Add automated tests for authentication, ownership boundaries, safe paths,
    server lifecycle behavior, and API contracts.

## Suggested delivery order

1. Close authorization gaps and deploy only through HTTPS.
2. Complete browser token renewal/logout and route protection.
3. Make the existing lifecycle, console, metrics, and file APIs usable from the
   web panel.
4. Harden server creation and align supported software choices between backend
   and UI.
5. Implement roles and administrator workflows.
6. Add scheduler/automation and plugin/mod management as new subsystems.
