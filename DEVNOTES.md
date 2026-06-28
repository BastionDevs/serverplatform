# Development notes

This file records backend behavior that the Blazor frontend deliberately makes
available even though the implementation is incomplete or unsafe. Treat these
items as development functionality, not as a production security boundary.

## Exposed backend risks

### File manager

The page at `/servers/{id}/files` exposes every implemented file route.

- Path containment uses a plain case-insensitive string prefix. A resolved
  sibling path whose name starts with the expected root can pass the check. This
  affects all file operations, including the owner-checked routes.
- The backend has no upload limit, quota, text-file size limit, or download size
  limit. The browser client imposes a 64 MB upload cap and buffers uploads and
  downloads in memory; large transfers can still exhaust browser memory.
- Delete recursively removes directories. Move can overwrite files when the
  user enables the option. Neither operation has a recycle bin or undo.
- The text editor attempts to decode any selected file as text. Binary and very
  large files may produce unusable output or memory pressure.

Do not expose this panel to mutually untrusted accounts until separator-aware
containment checks are fixed in the backend.

### Server creation and lifecycle

The dashboard and server detail pages expose creation, deletion, start, stop,
restart, and metrics.

- Server creation is not transactional. Failed runtime/JAR work may leave an
  index entry or partial directory, and some internal failures can still be
  reported as success.
- Runtime installation is triggered during creation but its asynchronous result
  is not reliably awaited by the backend.
- Inputs do not have a formal validation schema. The frontend offers Paper,
  Vanilla, Velocity, and manually identified locally imported Spigot/Bukkit
  JARs and performs basic RAM validation, but the backend remains authoritative
  and permissive. A missing or mistyped imported version can leave partial state.
- Stop and restart acknowledge that work was queued, not that the final state
  was reached. Metrics failures are interpreted by the frontend as offline.
- Server deletion recursively removes the complete server directory and cannot
  be undone.

### Console

The page at `/servers/{id}/console` exposes authenticated SSE output and command
submission.

- The command handler sends no normal HTTP success response. The frontend
  cancels the request after three seconds and assumes the command may have been
  accepted; it cannot confirm execution.
- Console history is intentionally temporary and scoped to the current server
  process. The bounded buffer is replayed when the console is opened and cleared
  when a new server process starts. It is not an audit log.
- Commands have no role or command-level authorization beyond server ownership.

### Authentication

Login, registration, refresh rotation, and logout are available in the frontend.

- Access and refresh tokens are stored in browser `localStorage`; an XSS flaw in
  the panel could steal both.
- The default deployment is plain HTTP. Credentials, tokens, console data, and
  files are readable in transit unless TLS is provided by a trusted proxy.
- Registration is public. There are no roles, administrator policies, password
  recovery, rate limits, account controls, or session-management screens.
- CORS reflects requesting origins instead of using an explicit allowlist.
### API contract and backend information

The page at `/backend` exposes `/endpointinfo`. The backend's public profile
lookup is deliberately not exposed by the frontend.

- `/endpointinfo` reports a hard-coded server name and version rather than build
  metadata.
- Responses use inconsistent success types, status codes, and error shapes. The
  frontend contains compatibility parsing for boolean and string success values.
- There is no API version prefix, OpenAPI contract, pagination, or compatibility
  guarantee.

## Production gate

At minimum, fix file path containment, deploy through HTTPS, replace permissive
CORS, secure first-run credentials/registration, add rate limiting and roles,
standardize API responses, and add integration tests before treating this panel
as production-safe.
