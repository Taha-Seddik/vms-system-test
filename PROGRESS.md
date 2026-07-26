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
| 7. Playback and keyframes | Not started | — |
| 8. Events, alarms, and incidents | Not started | — |
| 9. Users, search, and audit logs | Not started | — |
| 10. Final integration and documentation | Not started | — |

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
