# Video Management System Assessment

A lean full-stack Video Management System proof of concept built in gated steps
from the supplied technical assessment.

The current repository contains **Steps 1 through 7**: the application/media
foundation, Identity-based authentication and role authorization, Viewer
camera assignments, persistent camera management, FFprobe connection testing,
automatic health monitoring, heartbeats, connectivity events, and a real-time
operational command center, multi-camera HLS monitoring, and real FFmpeg
recording workflows with protected playback and keyframe navigation.

## Architecture

```text
Four FFmpeg generators
        │ RTSP/TCP
        ▼
     MediaMTX ────── HLS ──────► Browser / React frontend
        │
        └──── control API and metrics

React frontend ───── HTTP ─────► ASP.NET Core API ─────► PostgreSQL
```

| Service | Host URL/port | Purpose |
|---|---|---|
| Frontend | <http://localhost:3000> | React/TypeScript application |
| API | <http://localhost:8080> | ASP.NET Core API |
| API health | <http://localhost:8080/health> | API liveness check |
| MediaMTX HLS | <http://localhost:8888> | Browser-compatible live streams |
| MediaMTX API | <http://localhost:9997> | Local media-server health/control API |
| MediaMTX metrics | <http://localhost:9998/metrics> | Media pipeline metrics |
| RTSP | `rtsp://localhost:8554` | Camera ingest/read endpoint |
| PostgreSQL | `localhost:5432` | Application database |

The four HLS playlists are:

- <http://localhost:8888/camera-1/index.m3u8>
- <http://localhost:8888/camera-2/index.m3u8>
- <http://localhost:8888/camera-3/index.m3u8>
- <http://localhost:8888/camera-4/index.m3u8>

## Windows prerequisites

The assessment stack runs through Linux containers. On Windows, install:

1. **WSL 2**

   Confirm that hardware virtualization is enabled in BIOS/UEFI. Docker Desktop
   currently requires at least WSL 2.1.5 and 8 GB of system RAM.

   Open PowerShell as Administrator:

   ```powershell
   wsl --install
   ```

   Restart Windows when requested, then run:

   ```powershell
   wsl --update
   wsl --status
   ```

   Official guide: <https://learn.microsoft.com/windows/wsl/install>

2. **Docker Desktop**

   Install with WinGet:

   ```powershell
   winget install --exact --id Docker.DockerDesktop
   ```

   Start Docker Desktop, allow it to use the WSL 2 backend, and wait until the
   engine reports that it is running. Verify with:

   ```powershell
   docker version
   docker compose version
   ```

   Official guide:
   <https://docs.docker.com/desktop/setup/install/windows-install/>

3. **.NET 10 SDK**

   The containers build the API without a host SDK, but the SDK is needed for
   local backend builds and tests:

   ```powershell
   winget install --exact --id Microsoft.DotNet.SDK.10
   ```

   Open a new terminal and verify:

   ```powershell
   dotnet --version
   dotnet --list-sdks
   ```

   Official download: <https://dotnet.microsoft.com/download>

Git, Node.js, npm, FFmpeg, and FFprobe were already available on the original
development host.

## One-command startup

No `.env` file is required for the safe development defaults:

```powershell
docker compose up --build -d
```

To change ports or credentials, copy `.env.example` to `.env` first:

```powershell
Copy-Item .env.example .env
```

View status and logs:

```powershell
docker compose ps
docker compose logs -f
```

Stop the stack without deleting database data:

```powershell
docker compose down
```

## Verification

After the containers are healthy:

```powershell
.\scripts\verify-foundation.ps1
```

The script verifies:

- API and frontend health endpoints;
- all four HLS playlists;
- an actual decodable video stream behind every playlist using FFprobe inside
  the camera-generator container;
- running and healthy Compose services.

Verify Step 2 authentication and authorization:

```powershell
.\scripts\verify-auth.ps1
```

Verify Step 3 camera management and health:

```powershell
.\scripts\verify-camera-health.ps1
```

The Step 3 script checks persisted camera groups and cameras, assignment
filtering, role boundaries, real FFprobe metadata, all four automatic
heartbeats, camera and group CRUD, enable/disable, and a real
offline-to-reconnected event transition. Temporary verification resources are
cleaned up automatically.

Verify Step 4 command center aggregation and real-time updates:

```powershell
.\scripts\verify-command-center.ps1
```

The Step 4 script checks role boundaries, the complete operational snapshot,
actual recording-volume capacity, active-user and uptime metrics, alarm and
incident classification, and a real SignalR notification caused by a real
FFprobe camera test. Its temporary failure event is removed automatically.

Verify Steps 5 and 6 live monitoring access and real recording media:

```powershell
.\scripts\verify-live-recording.ps1
```

The combined script checks Viewer assignments and recording denial, all four
online wall cameras, manual recording and mode conflicts, automatic
motion-event recording, multiple continuous segments, actual H.264 MP4 media
through FFprobe, a real unavailable-source failure, command-center failure
visibility, and camera-source recovery.

Verify Step 7 playback, downloads, seeking, filters, and keyframes:

```powershell
.\scripts\verify-playback.ps1
```

The script reuses a completed recording longer than 30 seconds or creates one.
It verifies playback role boundaries, HTTP range support, protected MP4 media,
safe downloads, camera/type/status filters, invalid-date validation, and real
decodable JPEG keyframes at 0 and 30 seconds.

Verify Step 8 events, alarms, incidents, and real-time closing:

```powershell
.\scripts\verify-events.ps1
```

The script checks Viewer denial, invalid dates, all eight assessment event
types and required fields, combined filters, details, alarm/incident
classification, the close lifecycle, command-center consistency, a real
SignalR update, and automatic cleanup of temporary records.

Local source checks:

```powershell
dotnet test Vms.slnx

Push-Location frontend
npm ci
npm run lint
npm test
npm run build
Pop-Location
```

## Step 2 demo accounts

These accounts are seeded only when the user table is empty:

| Role | Username | Password | Camera access |
|---|---|---|---|
| Administrator | `admin` | `Admin123!` | All four cameras plus activity |
| Operator | `operator` | `Operator123!` | All four cameras |
| Viewer | `viewer` | `Viewer123!` | Assigned `camera-1` and `camera-2` only |

The passwords above are local assessment credentials. ASP.NET Core Identity
manages users, normalized usernames, salted password hashes, failed-access
counts, lockout, security stamps, roles, and role membership in PostgreSQL.
The VMS adds its own short-lived JWT and revocable session record because the
approved assessment plan explicitly requires JWT plus active-user/logout
tracking. JWT signing configuration can be overridden through the `JWT_ISSUER`,
`JWT_AUDIENCE`, and `JWT_SIGNING_KEY` values in an untracked `.env` file.

Verify authentication, roles, assignments, revocation, activity, and database
storage against the running Docker stack:

```powershell
.\scripts\verify-auth.ps1
```

Authentication endpoints:

| Endpoint | Access | Purpose |
|---|---|---|
| `POST /api/auth/login` | Anonymous | Verify credentials and create a session/JWT |
| `POST /api/auth/logout` | Authenticated | Revoke the current session |
| `GET /api/auth/me` | Authenticated | Return current user and assignments |
| `GET /api/auth/activity` | Administrator | Active sessions and login/logout events |
| `GET /api/cameras/accessible` | Authenticated | Return the role-authorized camera list |

## Step 3 camera management and health

Camera configuration is stored in PostgreSQL. The API container includes
FFprobe and checks enabled RTSP sources every 15 seconds. A successful probe
updates resolution, FPS, online status, and last heartbeat. A failed probe
updates offline status and its safe error message. Status transitions create
Camera Offline and Camera Reconnected system events.

| Endpoint | Access | Purpose |
|---|---|---|
| `GET /api/cameras` | Authenticated | Assignment-aware camera list and health |
| `GET /api/cameras/manage` | Administrator | Full camera configuration list |
| `POST /api/cameras` | Administrator | Add a camera |
| `PUT /api/cameras/{id}` | Administrator | Edit a camera |
| `DELETE /api/cameras/{id}` | Administrator | Delete a camera |
| `PATCH /api/cameras/{id}/enabled` | Administrator | Enable or disable a camera |
| `POST /api/cameras/{id}/test-connection` | Administrator, Operator | Run a bounded FFprobe test |
| `GET /api/camera-groups` | Authenticated | List groups |
| `POST/PUT/DELETE /api/camera-groups` | Administrator | Manage groups |

Administrators can use the Material UI management workspace at
<http://localhost:3000/manage/cameras>. Operators can run connection tests from
the normal camera screen. Viewers receive status only for their assigned
cameras and cannot run probes or mutate configuration.

## Step 4 command center dashboard

Administrators and Operators land on
<http://localhost:3000/command-center>. One aggregated API read supplies camera,
stream, recording, active-user, uptime, storage, alarm, incident, failure, and
operator-activity data. SignalR invalidates the snapshot after relevant
authentication, camera-management, connection-test, or health-monitor changes;
the browser then reloads authoritative data. A 30-second REST poll remains as a
fallback.

The authenticated application uses a fixed left administration sidebar on
desktop and a solid mobile header with an off-canvas menu. Operational list
cards show at most five rows; their View details/View all actions open a
right-side drawer with the complete result returned by the command-center API.
Camera and camera-group management remain together in one Administrator
workspace. The application uses a simple light palette with white surfaces,
soft-gray page backgrounds, and one blue accent. Sidebar contents are
constrained to the Drawer width, and long navigation text truncates rather than
causing horizontal scrolling.

The dashboard uses these measurable definitions:

- active users are distinct, enabled, unrevoked sessions seen in the last five
  minutes;
- active live streams are enabled cameras currently confirmed Online by
  FFprobe;
- active alarms are open Warning or Critical events;
- recent incidents are operational events rather than login/logout activity;
- storage health comes from the mounted recording volume, with warning and
  critical thresholds configurable in `.env`;
- uptime begins at API process start.

| Endpoint | Access | Purpose |
|---|---|---|
| `GET /api/command-center` | Administrator, Operator | Complete dashboard snapshot |
| `/hubs/command-center` | Administrator, Operator | SignalR snapshot-invalidation notifications |

## Step 5 multi-camera live monitoring

The live-monitoring workspace at <http://localhost:3000/cameras> supports
1/4/9/16 layouts. HLS.js attaches each MediaMTX playlist to an HTML video
element, with native HLS fallback where available. Every populated tile shows
camera identity, connection and recording state, fullscreen, 1x/1.5x/2x
digital zoom, and a current-frame PNG snapshot. Empty 9/16 cells are shown
explicitly because the assessment uses four demo cameras.

The camera-list API remains the authorization boundary: Administrators and
Operators receive all cameras, while the seeded Viewer receives only camera-1
and camera-2. Recording controls are absent for Viewers and independently
protected by the API.

## Step 6 real recording workflows

Administrators and Operators can start manual or continuous recording, simulate
motion, and stop a recording from the live wall. FFmpeg reads the actual RTSP
source and writes GUID-named MP4 files to the persistent `recording-data`
volume. FFprobe must confirm a video stream, positive duration, and file size
before metadata is marked Completed.

Continuous recording produces configurable ten-second playable segments.
Simulated motion persists a real Motion Detected event and triggers an
automatic configurable eight-second event recording. A camera can have only
one active mode; conflicts return `409`. Failures are persisted as critical
recording-failure events and appear on the command center.

| Endpoint | Access | Purpose |
|---|---|---|
| `GET /api/recordings` | Administrator, Operator | Recent recording metadata, optionally filtered by camera |
| `POST /api/cameras/{id}/recordings/manual/start` | Administrator, Operator | Start manual FFmpeg capture |
| `POST /api/cameras/{id}/recordings/continuous/start` | Administrator, Operator | Start segmented continuous capture |
| `POST /api/cameras/{id}/motion/simulate` | Administrator, Operator | Persist motion event and start real event capture |
| `POST /api/cameras/{id}/recordings/stop` | Administrator, Operator | Gracefully finalize the active capture |

## Step 7 playback and keyframes

Administrators and Operators can open
<http://localhost:3000/playback> to browse completed recordings. Filters cover
camera, manual/continuous/event mode, and start-date range. The selected MP4 is
loaded through an authenticated API request; JWTs are not placed in media URLs.

Playback includes the native video controls plus an explicit timeline,
play/pause, ten-second backward/forward seeking, 0.5x/1x/1.5x/2x/4x speed
control, current-frame PNG snapshot, and original MP4 download.

FFmpeg generates JPEG previews at zero seconds and every configurable 30
seconds afterward. Existing completed recordings are backfilled automatically.
Selecting a thumbnail seeks the player directly to its stored timestamp.

| Endpoint | Access | Purpose |
|---|---|---|
| `GET /api/recordings` | Administrator, Operator | Filter by camera, date, mode, state, and result limit |
| `GET /api/recordings/{id}` | Administrator, Operator | Recording detail and ordered keyframe timeline |
| `GET /api/recordings/{id}/media` | Administrator, Operator | Protected range-enabled MP4 response |
| `GET /api/recordings/{id}/download` | Administrator, Operator | Protected MP4 attachment |
| `GET /api/recordings/{id}/keyframes/{keyframeId}` | Administrator, Operator | Protected JPEG preview |

## Step 8 events, alarms, and incidents

Administrators and Operators can open <http://localhost:3000/events> to browse
the live event panel. Filters cover date, camera, event type, severity, and
Open/Closed status. Selecting an event shows its complete assessment fields;
an Open event can be closed from the details drawer.

An active alarm is exactly an Open Warning or Critical event. An incident is
any operational event other than User Login or User Logout, so incidents reuse
the event lifecycle instead of duplicating records. SignalR invalidations
refresh both Events and Command Center screens, with a 30-second polling
fallback.

The required Storage Full event is generated automatically when the real
recording-volume status reaches Critical. The 30-second monitor creates only
one open event and closes it when capacity recovers.

| Endpoint | Access | Purpose |
|---|---|---|
| `GET /api/events` | Administrator, Operator | Filter events and return event/alarm/incident counts |
| `GET /api/events/{id}` | Administrator, Operator | Read one event with complete fields and classifications |
| `POST /api/events/{id}/close` | Administrator, Operator | Persist Closed status and publish a SignalR update |

## Repository layout

```text
backend/
  Vms.Api/                 Lean layered ASP.NET Core API
    Controllers/           HTTP routes, status codes, authorization attributes
    Services/              Identity workflows, sessions, JWTs, camera access
    Models/                API request/response and configuration models
    Domain/                Identity user extension and VMS domain entities
    Extensions/            DI, authentication, policies, claims, persistence
    Utils/                 Small reusable stateless helpers
    Data/                  EF Core context, migrations, and seed data
  Vms.Api.Tests/           API integration tests
frontend/                  React, TypeScript, Vite, Material UI
infra/
  camera-generator/        FFmpeg-generated RTSP source image
  mediamtx/                MediaMTX image and configuration
scripts/
  verify-foundation.ps1    End-to-end Step 1 verification
  verify-auth.ps1          End-to-end Step 2 authorization verification
  verify-camera-health.ps1 End-to-end Step 3 camera/health verification
  verify-command-center.ps1 End-to-end Step 4 dashboard/SignalR verification
  verify-live-recording.ps1 End-to-end Steps 5/6 access and media verification
  verify-playback.ps1      End-to-end Step 7 playback/keyframe verification
  verify-events.ps1        End-to-end Step 8 event/alarm verification
docs/
  index.html               HTML implementation documentation home
  assets/                  Shared documentation styling
  steps/                   One implementation guide per approved step
compose.yaml               Complete local stack
PROGRESS.md                Gated assessment progress
plan.md                    Approved implementation plan
```

## Implementation documentation

Open [`docs/index.html`](docs/index.html) in a browser to read the assessment
implementation guides. Every completed step receives a formatted HTML guide
covering its requirements, architecture, dependencies, important code,
verification evidence, limitations, and next-step context.

The Step 1 guide is:
[`docs/steps/step-01-repository-media-foundation.html`](docs/steps/step-01-repository-media-foundation.html).

The Step 2 guide is:
[`docs/steps/step-02-authentication-roles-assignments.html`](docs/steps/step-02-authentication-roles-assignments.html).

The Step 3 guide is:
[`docs/steps/step-03-camera-management-health.html`](docs/steps/step-03-camera-management-health.html).

The Step 4 guide is:
[`docs/steps/step-04-command-center-dashboard.html`](docs/steps/step-04-command-center-dashboard.html).

The Step 5 guide is:
[`docs/steps/step-05-multi-camera-live-monitoring.html`](docs/steps/step-05-multi-camera-live-monitoring.html).

The Step 6 guide is:
[`docs/steps/step-06-real-recording-workflows.html`](docs/steps/step-06-real-recording-workflows.html).

The Step 7 guide is:
[`docs/steps/step-07-playback-keyframes.html`](docs/steps/step-07-playback-keyframes.html).

The Step 8 guide is:
[`docs/steps/step-08-events-alarms-incidents.html`](docs/steps/step-08-events-alarms-incidents.html).

## Development configuration

Development defaults are intentionally non-secret. Do not reuse them outside a
local assessment environment. Real secrets belong in an untracked `.env` file
or a secret manager.

The generated feeds use 640×360 H.264 video at 10 FPS and 500 Kbit/s so four
feeds can run comfortably on a typical laptop. Each feed has a unique label and
color treatment.

## Scope

User administration, global search, and full audit-log management remain gated
by the approved plan. The raw local MediaMTX HLS port is not yet protected;
media authorization hardening is scheduled for Step 10.
