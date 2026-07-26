# VMS Assessment Progress

This file records the gated implementation state. No later step begins until the
current step has been reviewed and explicitly approved.

| Step | Status | Evidence |
|---|---|---|
| 1. Repository and media foundation | Completed and verified | Git repository, API/frontend/test scaffolds, healthy Compose topology, four decodable HLS feeds, verification script, HTML implementation guide |
| 2. Authentication, roles, and assignments | Completed and verified | ASP.NET Core Identity, layered backend, JWT sessions, three Identity roles, lockout, persisted Viewer assignments, login/logout events, protected React routes, 13 backend tests, 3 frontend tests, live verification script, HTML guide |
| 3. Camera management and health | Completed and verified | Persistent cameras/groups, Administrator CRUD UI/API, FFprobe tests, automatic status and heartbeat monitoring, offline/reconnected events, 17 backend tests, 4 frontend tests, clean migration proof, live verification script, HTML guide |
| 4. Command center dashboard | Completed and verified | Aggregated operational snapshot, measurable metrics, recording-volume health, Administrator/Operator dashboard, SignalR invalidation with polling fallback, 20 backend tests, 5 frontend tests, live verification script, HTML guide |
| 5. Multi-camera live monitoring | Completed and verified | 1/4/9/16 HLS wall, assignment enforcement, status/REC overlays, fullscreen, zoom, snapshot, frontend tests, real stream verification, HTML guide |
| 6. Real recording workflows | Completed and verified | Manual/continuous/event FFmpeg capture, playable FFprobe-validated MP4s, persisted lifecycle/failures, 25 backend tests, live verification, HTML guide |
| 7. Playback and keyframes | Completed and verified | Protected MP4 playback/download, recording filters, timeline controls, snapshots, FFmpeg JPEGs every 30 seconds, clickable keyframe seeking, 28 backend tests, 7 frontend tests, live verifier, HTML guide |
| 8. Events, alarms, and incidents | Completed and verified | Required event types/fields, filtering, details, close lifecycle, automatic Storage Full monitoring, alarm/incident rules, SignalR updates, 34 backend tests, 8 frontend tests, live verifier, HTML guide |
| 9. Users, search, and audit logs | Completed and verified | Identity user administration, roles and Viewer assignments, four-category search, required filters, durable audit logs, operator activity, 39 backend tests, 11 frontend tests, clean migration, live verifier, HTML guide |
| 10. Final integration and documentation | Completed and verified | Assignment-protected HLS, internal-only media services, Swagger/OpenAPI, RTSP credential redaction, Command Center live video wall, security headers, 44 backend tests, 12 frontend tests, live delivery verifier, architecture diagram, final checklist, and HTML guide |

## Step 1 acceptance evidence

- `docker compose up --build -d` is the single startup command.
- `scripts/verify-foundation.ps1` checks API/frontend health, all four HLS
  playlists, decodable media, and container health.
- Frontend dependencies, linting, tests, and production build passed.
- Backend tests passed.
- Complete Compose runtime verification passed on 2026-07-26: all eight
  services were healthy and every HLS feed decoded as H.264, 640×360 at 10 FPS.
- `docs/steps/step-01-repository-media-foundation.html` documents the
  architecture, dependencies, implementation excerpts, proof, boundaries, and
  concepts required before Step 2.

## Step 2 acceptance evidence

- ASP.NET Core Identity manages user/password storage, normalization, security
  stamps, failed-access lockout, roles, and role membership; plaintext
  passwords are never stored.
- The Identity adoption and security-stamp backfill migrations succeeded both
  against the existing Step 2 database and a temporary clean PostgreSQL
  database.
- Signed JWTs carry user, role, and session identity; the API verifies the
  persisted session and enabled account on every authenticated request.
- Administrator, Operator, and Viewer API policies were verified with exact
  `200`, `401`, and `403` responses.
- The demo Viewer has two persisted assignments and receives exactly
  `camera-1,camera-2`; a Viewer with no assignments cannot sign in.
- Logout immediately revokes the current token. Login/logout system events and
  recent activity are persisted and available to Administrators.
- React provides protected routes, role-aware navigation, session validation,
  Viewer-filtered cameras, and an Administrator-only activity screen.
- Backend responsibilities are organized into explicit `Controllers`,
  `Services`, `Models`, `Domain`, `Extensions`, and `Data` layers; `Program.cs`
  contains only application composition and middleware.
- `dotnet test Vms.slnx`: 13 passed; frontend tests: 3 passed.
- Backend build completed with 0 warnings/0 errors; ESLint and the frontend
  production build passed.
- `scripts/verify-auth.ps1` passed against PostgreSQL and the running Compose
  stack; all eight services were healthy.
- Step 1 foundation verification passed again with four decodable HLS feeds.
- `docs/steps/step-02-authentication-roles-assignments.html` documents the
  architecture, dependencies, code, proof, tradeoffs, and Step 3 handoff.

## Step 3 acceptance evidence

- Camera and camera-group records are persisted through EF Core. The four demo
  feeds are seeded as cameras in Perimeter and Operations groups.
- Administrator camera/group CRUD, enable/disable, and management reads were
  verified through APIs and focused backend tests.
- Viewer assignment filtering still returns exactly `camera-1,camera-2`.
  Viewers receive `403` for connection tests and management mutations.
- FFprobe is installed in the API image, invoked without a shell, and bounded
  by a configurable timeout. Live probes returned H.264, 640x360, and 10 FPS.
- The 15-second background monitor marked all four cameras Online with
  non-null last-heartbeat timestamps.
- A temporary unavailable RTSP source generated exactly one open warning
  Camera Offline event. Repairing it generated exactly one closed information
  Camera Reconnected event.
- The existing database upgraded successfully. A clean PostgreSQL database
  applied all four migrations and contained four cameras and two groups.
- `dotnet test Vms.slnx`: 17 passed; frontend tests: 4 passed.
- Backend release build completed with 0 warnings/0 errors; frontend lint and
  production build passed.
- All eight Compose services were healthy and the four HLS feeds remained
  decodable after the API image gained FFprobe.
- `scripts/verify-camera-health.ps1` passed and removed its temporary camera
  and group after verification.
- `docs/steps/step-03-camera-management-health.html` explains the architecture,
  dependencies, code, proof, tradeoffs, and Step 4 handoff.

## Step 4 acceptance evidence

- `GET /api/command-center` returns one authoritative snapshot containing all
  assessment dashboard and central-command-center categories.
- Active users, live streams, active recordings, active alarms, incidents,
  operator activity, storage health, and uptime have explicit measurable
  definitions rather than display-only placeholders.
- Storage capacity and usage are read from the mounted recording volume;
  warning and critical thresholds are configurable.
- Administrators and Operators can access the command center. Viewers receive
  `403`, and anonymous SignalR negotiations receive `401`.
- SignalR notifications are published after relevant login/logout, camera,
  group, connection-test, and health-monitor actions. Clients reload the
  authoritative REST snapshot and fall back to a 30-second poll.
- The responsive React dashboard includes metrics, camera health, storage,
  offline cameras, recording failures, active alarms, recent incidents,
  operator activity, and recent events with explicit empty states.
- The reviewed application shell now uses a fixed left administration sidebar
  on desktop and a solid mobile header/drawer. Camera and group management
  remain together in one workspace.
- The application palette is now a simple light administration theme. Drawer
  contents use constrained widths, clipped horizontal overflow, and ellipsis
  for long labels, eliminating sidebar horizontal scrolling.
- Operational cards show no more than five preview rows. Focused tests verify
  that View all opens the full result in a right-side detail drawer and that
  the drawer closes accessibly.
- `dotnet test Vms.slnx`: 20 passed; frontend tests: 5 passed.
- Backend release build completed with 0 warnings/0 errors; frontend lint and
  production build passed.
- `scripts/verify-command-center.ps1` passed against the running Compose stack.
  It verified a temporary critical recording-failure alarm and received a real
  SignalR update triggered by a real FFprobe camera test, then cleaned up.
- Steps 1, 2, and 3 verification scripts passed again; all eight Compose
  services were healthy and all four HLS streams remained decodable.
- `docs/steps/step-04-command-center-dashboard.html` explains requirements,
  metric semantics, architecture, dependencies, code, proof, boundaries, and
  the Step 5 handoff.

## Step 5 acceptance evidence

- The React live wall implements exactly the required 1/4/9/16 layouts, with
  honest empty cells when four demo cameras do not fill a larger layout.
- HLS.js attaches each MediaMTX playlist to a native video element and includes
  native-HLS fallback plus bounded network/media recovery.
- Tiles expose camera identity, health metadata, REC state, fullscreen,
  1x/1.5x/2x digital zoom, and current-frame PNG snapshot.
- Viewer access remains enforced by the assignment-aware backend: the seeded
  Viewer receives exactly camera-1 and camera-2.
- All four HLS cameras were probe-confirmed Online; the foundation media check
  confirms decodable H.264, 640x360, 10 FPS output.
- Frontend lint passed; all 6 frontend tests passed; production build passed.
- `docs/steps/step-05-multi-camera-live-monitoring.html` explains the media
  flow, authorization boundary, controls, dependency, proof, and limitations.

## Step 6 acceptance evidence

- Manual recording starts and stops a real FFmpeg process and produces a
  finalized MP4 with positive duration and size.
- Continuous mode creates consecutive configurable ten-second MP4 segments;
  at least two independently playable segments were verified.
- Simulated motion creates a persisted Motion Detected event and triggers a
  real automatically bounded event MP4.
- FFprobe validates every completed file; empty/header-only fragments are not
  reported as successful recordings.
- Only one recording mode may own a camera at once. Operators and
  Administrators are authorized; Viewer recording requests return `403`.
- Recording metadata, owner, trigger link, state, duration, size, and safe
  failure details persist in PostgreSQL. Files persist on the recording volume.
- All migrations created the recording schema in a temporary clean PostgreSQL
  database, which was removed after the check.
- A deliberately broken RTSP source produced a real Failed row and critical
  command-center recording failure; the source was restored and reconnected.
- `dotnet test Vms.slnx --no-restore`: 25 passed. Frontend lint, 6 tests, and
  production build passed.
- `scripts/verify-live-recording.ps1` passed end to end against the running
  Docker stack; all eight services were healthy.
- Fifteen completed recording rows and playable volume media were confirmed
  unchanged after an API container restart.
- `docs/steps/step-06-real-recording-workflows.html` explains modes, lifecycle,
  backend layers, process safety, proof, and the gated Step 7 handoff.

## Step 7 acceptance evidence

- Administrators and Operators can browse completed recordings; anonymous
  users receive `401` and Viewers receive `403`.
- Recording queries filter by camera, date range, mode, and lifecycle state.
  Reversed date ranges return `400`.
- MP4 media and downloads are served only through authenticated endpoints.
  Media responses support HTTP byte ranges, and download names are generated
  safely by the server.
- The React workspace includes native playback plus an explicit timeline,
  play/pause, ten-second back/forward seeking, 0.5x through 4x speed controls,
  current-frame PNG snapshot, and MP4 download.
- FFmpeg creates real JPEG thumbnails at 0 seconds and every configured 30
  seconds. A verified long recording produced decodable previews at 0 and 30.
- Clicking the 30-second thumbnail set the video current time to 30 seconds in
  the frontend interaction test. The same test created a PNG snapshot, fetched
  the MP4 download, and changed playback speed to 4x.
- Existing completed recordings are backfilled by a bounded background service;
  requesting details also guarantees missing keyframes synchronously.
- All migrations created the keyframe schema in a temporary clean PostgreSQL
  database, which was removed after verification.
- Two real keyframe rows remained stable across an API container restart.
- The recording and keyframe paths are resolved inside the configured storage
  root from server-generated names; client filesystem paths are never accepted.
- `dotnet test Vms.slnx --no-restore`: 28 passed. Frontend lint, 7 tests, and
  production build passed.
- `scripts/verify-playback.ps1` passed against real PostgreSQL, FFmpeg, FFprobe,
  MP4 range/download responses, and JPEG keyframe files.
- `docs/steps/step-07-playback-keyframes.html` explains requirements, playback
  flow, security, keyframe generation, code, proof, and the Step 8 handoff.

## Step 8 acceptance evidence

- Camera Offline, Motion Detected, Recording Started, Recording Stopped,
  Storage Full, Camera Reconnected, User Login, and User Logout are supported.
  Recording Failure remains as an assessment-required operational extension.
- Every API event includes timestamp, camera identity or explicit system-wide
  context, severity, description, and Open/Closed status.
- Administrators and Operators can filter by date, camera, event type,
  severity, and status, inspect complete details, and close Open events.
  Viewers receive `403` for both reads and close operations.
- An active alarm has one exact rule: Open plus Warning or Critical severity.
  Incidents reuse operational events and exclude only login/logout activity.
- The storage monitor evaluates real recording-volume health every 30 seconds,
  creates one Open Critical Storage Full event at the critical threshold, and
  closes it after recovery. A focused test verified both transitions.
- The Events page receives SignalR invalidations and reloads authoritative REST
  data, with the same 30-second polling fallback as the command center.
- `dotnet test Vms.slnx --no-restore`: 34 passed. Frontend lint, 8 tests, and
  production build passed.
- `scripts/verify-events.ps1` passed against the Docker stack. It verified all
  eight required event types/fields, filters, details, command-center alarm and
  incident consistency, close persistence, and a real `event-closed` SignalR
  message, then removed all temporary events.
- `docs/steps/step-08-events-alarms-incidents.html` explains requirements,
  classification rules, runtime flows, code, proof, limitations, and the gated
  Step 9 handoff.

## Step 9 acceptance evidence

- Administrator-only user management creates, updates, disables, password
  resets, changes roles, deletes, and searches ASP.NET Core Identity users.
- Viewer creation/update requires at least one valid camera assignment.
  Administrator and Operator accounts cannot retain Viewer assignments.
- Password, role, and disabled-state changes revoke active sessions. The
  current Administrator cannot delete, disable, or demote their own account.
- `GET /api/search` returns grouped camera, recording, event, and
  Administrator-only user results. It supports text, date, camera, camera
  group, status, and event-type filters with bounded result sizes.
- Operators can search operational resources but receive no user records.
  Viewers receive `403` for system-wide search.
- Login/logout and successful authenticated POST/PUT/PATCH/DELETE operations
  create durable audit rows containing actor, action, resource, timestamp, and
  description. Failed writes are not marked successful.
- The Administrator Audit Activity page filters by actor, resource, action,
  and date. The Command Center now shows recent audited Operator actions.
- A clean temporary PostgreSQL database applied every migration and created
  the indexed `AuditLogs` table; the temporary database was removed.
- `dotnet test Vms.slnx --no-restore`: 39 passed with zero warnings. Frontend
  lint, 11 tests, and production build passed.
- `scripts/verify-users-search-audit.ps1` passed against the Docker stack. It
  completed the full temporary-user lifecycle, assignment enforcement, session
  revocation, four-category search, filters, audit persistence, and
  command-center operator-activity checks, then deleted the temporary user.
- `docs/steps/step-09-users-search-audit.html` explains requirements, security
  flows, search semantics, audit behavior, code, proof, boundaries, and the
  gated Step 10 handoff.

## Step 10 acceptance evidence

- MediaMTX uses its supported external HTTP authentication flow for HLS reads.
  HLS.js adds the current bearer token to playlists and segments.
- The API validates JWT signature, issuer, audience, lifetime, the persisted
  active session, enabled account, role, enabled camera, and Viewer assignment
  before permitting media.
- Live verification returned `401` for anonymous camera 1 and Viewer camera 3,
  while assigned Viewer camera 1 and Operator camera 3 returned `200`.
- Authenticated HLS decoded as H.264, 640x360 at 10 FPS through FFprobe.
- RTSP, MediaMTX control API, and metrics are internal-only. Published
  development services bind to loopback.
- Swagger UI is available at `/swagger`; the bearer-aware OpenAPI document is
  available at `/openapi/v1.json`.
- The Command Center camera wall now renders four real authenticated HLS tiles
  rather than status placeholders.
- RTSP usernames/passwords are redacted from management responses. A redacted
  edit preserves the stored source, verified both in integration tests and
  against PostgreSQL.
- HLS path traversal variants are rejected. API and frontend responses include
  content-type, frame, referrer, permissions, and content-security headers.
- `dotnet test Vms.slnx --no-restore`: 44 passed. Frontend lint, 12 tests, and
  production build passed.
- `scripts/verify-delivery.ps1` passed against Docker and removed its temporary
  credential-test camera and sessions.
- The updated foundation verifier passed with all four authenticated,
  decodable HLS feeds and all eight healthy services.
- `docs/assets/vms-architecture.svg` documents the final runtime boundaries.
- `docs/steps/step-10-final-integration-delivery.html` contains dependencies,
  flows, code excerpts, final proof, assumptions, and the complete PDF
  deliverables checklist.
