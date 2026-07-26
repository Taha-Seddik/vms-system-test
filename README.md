# Video Management System (VMS)

## Intro

Full-stack Video Management System built for the Senior Full Stack Developer
technical assessment. It provides role-based access, camera management, live
monitoring, real recording and playback, events, search, audit logs, and a
real-time operational dashboard.

Four local FFmpeg camera generators are included, so the complete system can be
reviewed without physical cameras or unreliable public RTSP feeds.

## Main Features

- ASP.NET Core Identity, JWT authentication, and Administrator/Operator/Viewer roles
- Viewer-to-camera assignments
- Camera groups, CRUD, FFprobe connection testing, and automatic health monitoring
- Protected HLS live monitoring with 1/4/9/16 camera layouts
- Manual, continuous, and motion-event FFmpeg recording to playable MP4 files
- Recording playback, seeking, speed control, snapshots, downloads, and JPEG keyframes
- Events, alarms, incidents, user management, global search, and audit logs
- SignalR real-time dashboard updates with polling fallback
- Swagger/OpenAPI and Docker Compose deployment

## Tech Stack

- **Backend:** ASP.NET Core 10, Entity Framework Core, ASP.NET Core Identity, SignalR
- **Frontend:** React, TypeScript, Vite, Material UI, HLS.js
- **Database:** PostgreSQL
- **Media:** MediaMTX, FFmpeg, FFprobe
- **Deployment:** Docker Compose
- **Testing:** xUnit, Vitest, Testing Library, PowerShell integration scripts

## Requirements

For the normal Docker setup:

- Git
- Docker Desktop with Docker Compose
- At least 8 GB RAM recommended

.NET 10 SDK and Node.js 24 are only required when running source tests outside
Docker.

## How to Run the Project

```powershell
# 1. Clone and open the repository
git clone https://github.com/Taha-Seddik/vms-system-test.git
Set-Location vms-system-test

# 2. Build and start the complete stack
docker compose up --build -d

# 3. Check that all services are healthy
docker compose ps
```

The first startup can take several minutes while Docker downloads and builds
the images.

Open:

- Application: http://localhost:3000
- Swagger UI: http://localhost:8080/swagger
- OpenAPI JSON: http://localhost:8080/openapi/v1.json
- API health: http://localhost:8080/health

No `.env` file is required. To override the development defaults:

```powershell
Copy-Item .env.example .env
docker compose up --build -d
```

## Demo Accounts

| Role | Username | Password | Camera access |
|---|---|---|---|
| Administrator | `admin` | `Admin123!` | All cameras and administration |
| Operator | `operator` | `Operator123!` | All operational cameras |
| Viewer | `viewer` | `Viewer123!` | Entrance and Loading Bay only |

## Project Structure

```text
backend/
  Vms.Api/
    Controllers/             # REST endpoints
    Services/                # Authentication, cameras, recording, events
    Models/                  # API requests and responses
    Domain/                  # Database entities
    Extensions/              # DI, authentication, persistence, OpenAPI
    Middleware/              # Security and audit behavior
    Data/                    # EF Core context, migrations, seed data
    Utils/                   # Reusable helpers
  Vms.Api.Tests/             # Backend integration tests
frontend/                    # React/TypeScript application
infra/
  camera-generator/          # Four generated RTSP camera feeds
  mediamtx/                  # RTSP-to-HLS media server
scripts/                     # End-to-end verification scripts
docs/                        # Detailed implementation documentation
compose.yaml                 # Complete application stack
```

## Verification

Run the final delivery verification:

```powershell
.\scripts\verify-delivery.ps1
```

Run the complete assessment verification suite:

```powershell
$checks = Get-ChildItem .\scripts\verify-*.ps1 | Sort-Object Name
foreach ($check in $checks) {
    & $check.FullName
    if ($LASTEXITCODE -ne 0) { throw "$($check.Name) failed" }
}
```

Run source tests:

```powershell
dotnet test Vms.slnx

Push-Location frontend
npm ci
npm run lint
npm test
npm run build
Pop-Location
```

## Stop the Project

```powershell
docker compose down
```

Database records and recorded media remain in Docker volumes.

## Assessment Notes

- Motion detection is simulated, but it creates a real event and real MP4 recording.
- Live HLS streams require an authenticated, active session and enforce Viewer assignments.
- Events represent assessment-level incidents.
- Detailed architecture, implementation notes, and requirement evidence are available in
  [`docs/index.html`](docs/index.html).
