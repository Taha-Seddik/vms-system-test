# Video Management System Assessment

A lean full-stack Video Management System proof of concept built in gated steps
from the supplied technical assessment.

The current repository contains **Step 1 only**: the application skeleton and a
local media pipeline with four generated RTSP cameras exposed as HLS.

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

## Verify Step 1

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

## Repository layout

```text
backend/
  Vms.Api/                 Lean ASP.NET Core API
  Vms.Api.Tests/           API integration tests
frontend/                  React, TypeScript, Vite, Material UI
infra/
  camera-generator/        FFmpeg-generated RTSP source image
  mediamtx/                MediaMTX image and configuration
scripts/
  verify-foundation.ps1    End-to-end Step 1 verification
compose.yaml               Complete local stack
PROGRESS.md                Gated assessment progress
plan.md                    Approved implementation plan
```

## Development configuration

Development defaults are intentionally non-secret. Do not reuse them outside a
local assessment environment. Real secrets belong in an untracked `.env` file
or a secret manager.

The generated feeds use 640×360 H.264 video at 10 FPS and 500 Kbit/s so four
feeds can run comfortably on a typical laptop. Each feed has a unique label and
color treatment.

## Scope

Authentication, camera CRUD, dashboards, live playback UI, recording workflows,
events, users, search, and audit logs are intentionally not part of Step 1.
They remain gated by the approved plan.
