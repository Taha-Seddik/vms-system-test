# VMS Assessment — Gated Implementation Plan

## Summary

Build a polished, working proof of concept using React, ASP.NET Core, PostgreSQL, SignalR, MediaMTX, FFmpeg, and Docker Compose.

Work will proceed one step at a time. After every step, Codex will stop and provide:

1. **Assessment requirement recap** — what the PDF requested.
2. **Completed work** — what was implemented.
3. **Files/components changed** — the concrete implementation surface.
4. **Verification evidence** — builds, tests, API results, or stream checks.
5. **Limitations or deviations** — anything simplified and why.
6. **Next-step preview** — no next step starts without your confirmation.

Every completed step will also add or update a polished HTML implementation
guide under `docs/steps/`. Each guide will explain the requirement, architecture,
dependencies, important source excerpts, verification evidence, current
boundaries, and the concepts needed before the next step. `docs/index.html`
will link all completed step guides.

## Implementation Steps

### Step 1 — Repository and Media Foundation

- Initialize the Git repository and lean monorepo structure.
- Create the ASP.NET Core API, React frontend, and test projects.
- Add Docker Compose services for frontend, API, PostgreSQL, MediaMTX, and four generated RTSP cameras.
- Configure MediaMTX to expose the feeds through browser-compatible HLS.
- Add environment configuration, health checks, initial README, and progress tracking.

**Assessment proof:** Docker deployment foundation and working sources for the required multi-camera live view.

**Completion check:** One command starts the system, all containers become healthy, and four HLS playlists are reachable.

### Step 2 — Authentication, Roles, and Camera Assignment

- Add secure password hashing and JWT authentication.
- Add Administrator, Operator, and Viewer roles.
- Seed one local demo account for each role.
- Enforce permissions at both API and frontend-route levels.
- Add mandatory viewer-to-camera assignments.
- Record login and logout events and update user activity.

**Assessment proof:** Secure authentication, RBAC, and “Viewer can view assigned live cameras only.”

**Completion check:** All three users can log in, unauthorized requests are rejected, and the Viewer cannot access unassigned cameras or administrative functions.

### Step 3 — Camera Management and Health Monitoring

- Add camera and camera-group database models.
- Seed the four generated feeds as cameras.
- Implement camera list, create, edit, delete, enable, disable, and test-connection operations.
- Use FFprobe with a timeout for connection testing.
- Add a background health monitor for online status and last heartbeat.
- Generate camera-offline and camera-reconnected events.

**Assessment proof:** Camera Management, status monitoring, test connection, and last-heartbeat requirements.

**Completion check:** Camera operations work through the UI and API, connection checks return useful results, and status changes appear automatically.

### Step 4 — Command Center Dashboard

- Implement dashboard metrics for cameras, live streams, recordings, storage, active users, and system uptime.
- Display offline cameras, recording failures, recent events/incidents, active alarms, and operator activity.
- Define active users as users with recent authenticated activity.
- Define active alarms as open warning or critical events.
- Use SignalR for real-time updates, with 30-second polling as a fallback.

**Assessment proof:** Dashboard and Central Command Center requirements, including automatic refresh.

**Completion check:** Dashboard metrics match stored state and update without a page reload.

### Step 5 — Multi-Camera Live Monitoring

- Build 1, 4, 9, and 16-tile layouts.
- Play MediaMTX HLS streams using HLS.js with native HLS fallback.
- Display camera name, online status, and recording indicator.
- Add fullscreen, CSS-based digital zoom, and live snapshot download.
- Leave unused cells empty when there are fewer cameras than layout positions.
- Restrict Viewers to assigned cameras.

**Assessment proof:** Required multi-camera wall, live video controls, digital zoom, fullscreen, and snapshots.

**Completion check:** Four generated cameras play concurrently, every layout works, snapshots download, and Viewer restrictions remain enforced.

### Step 6 — Real Recording Workflows

- Add actual manual, continuous, and event recording modes.
- Manage FFmpeg processes behind a recording service.
- Store playable MP4 recordings and metadata in protected Docker volumes.
- Split continuous recording into finalized, playable segments.
- Let a simulated motion event trigger a real bounded recording.
- Emit recording-started, recording-stopped, and recording-failure events.
- Prevent duplicate or conflicting recording processes.

**Assessment proof:** Continuous, manual, and event-based recording—not placeholder controls.

**Completion check:** Each recording mode creates a playable file, failures create events, and state survives an API restart through database metadata and mounted storage.

### Step 7 — Playback and Keyframe Timeline

- Build recording browsing and filtering.
- Add authenticated playback and download endpoints.
- Implement play, pause, seek, fast-forward/speed controls, playback snapshot, and download.
- Generate JPEG keyframes every 30 seconds, including an initial frame for short recordings.
- Display thumbnail previews on the playback timeline.
- Seek playback when a keyframe is selected.

**Assessment proof:** Recording playback and intelligent keyframe timeline requirements.

**Completion check:** A completed recording plays, downloads, produces thumbnails, and seeks to the selected keyframe.

### Step 8 — Events, Alarms, and Incidents

- Support all required event types:
  - Camera Offline
  - Motion Detected
  - Recording Started
  - Recording Stopped
  - Recording Failure
  - Storage Full
  - Camera Reconnected
  - User Login
  - User Logout
- Store timestamp, camera, severity, description, and open/closed status.
- Build event listing, filtering, detail, and close actions.
- Treat open warning/critical events as active alarms and events as assessment-level incidents.
- Push new and updated events through SignalR.
- Clearly label motion detection as a simulation while keeping its recording real.

**Assessment proof:** Event Management, alarms, recent incidents, and real-time event-panel requirements.

**Completion check:** Events appear automatically, filters work, authorized users can close events, and simulated motion creates both an event and recording.

### Step 9 — Users, Search, and Audit Logs

- Add Administrator-only user creation, editing, enable/disable, role selection, and camera assignment.
- Complete search across cameras, recordings, events, and users.
- Support the required date, camera, group, status, and event-type filters where applicable.
- Record important write operations in the audit log.
- Display operator activity and audit records to Administrators.

**Assessment proof:** Users and Roles, Search and Filters, Audit Logs, and Operator Activity.

**Completion check:** Role restrictions are enforced, all four resources are searchable, required filters work, and write operations create audit entries.

### Step 10 — Integration, Security, Documentation, and Final Proof

- Complete Swagger/OpenAPI documentation.
- Validate input, redact RTSP credentials, protect stored media, and prevent unsafe FFmpeg/path arguments.
- Add focused backend and frontend tests for critical workflows.
- Polish responsive loading, error, empty, and confirmation states.
- Test from a clean Docker environment.
- Complete README setup instructions, architecture diagram, demo accounts, assumptions, limitations, screenshots, and feature matrix.
- Produce a final assessment-to-implementation checklist.

**Assessment proof:** Docker Deployment, REST API Documentation, README, security, and complete minimum-deliverable verification.

**Completion check:** A clean `docker compose up --build` starts the complete system and every assessment requirement has documented evidence.

## Important Interfaces

- REST APIs will cover authentication, cameras, dashboard, recordings, events, users, audit logs, storage, and health.
- SignalR will publish camera, recording, event, dashboard, and storage changes.
- Core shared types will include roles, recording modes/statuses, event types/severities/statuses, camera assignments, and filter contracts.
- Media files remain inaccessible directly; authenticated API endpoints provide playback, snapshots, keyframes, and downloads.

## Final Test Scenarios

- Administrator manages users, camera assignments, cameras, events, and recordings.
- Operator monitors cameras, records video, plays recordings, and manages incidents.
- Viewer sees only assigned live cameras.
- Camera failure updates its status and creates an event.
- Manual, continuous, and simulated-event recordings produce playable files.
- Keyframe selection seeks to the corresponding recording position.
- Dashboard and event panel update without manual refresh.
- Searches and filters return the expected cameras, recordings, events, and users.
- A clean Docker deployment works using only documented instructions.

## Assumptions

- React, ASP.NET Core, PostgreSQL, SignalR, Material UI, MediaMTX, and FFmpeg are the chosen stack, not technologies mandated by the PDF.
- Four generated local RTSP feeds avoid unreliable public-stream dependencies.
- Motion detection is simulated, but the resulting event recording is real.
- Four physical feeds are sufficient to demonstrate 1/4/9/16 layouts; larger layouts may contain empty cells.
- Events represent incidents, and open warning/critical events represent active alarms; no separate incident-management subsystem will be invented.
- The backend will use a pragmatic API plus test-project structure instead of a large multi-project clean-architecture design.
- Required functionality takes priority over optional visual polish if the 16–20 hour timebox becomes tight.
- Only one step will be implemented per confirmation gate.
