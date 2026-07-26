# VMS Assessment Progress

This file records the gated implementation state. No later step begins until the
current step has been reviewed and explicitly approved.

| Step | Status | Evidence |
|---|---|---|
| 1. Repository and media foundation | Completed and verified | Git repository, API/frontend/test scaffolds, healthy Compose topology, four decodable HLS feeds, verification script, HTML implementation guide |
| 2. Authentication, roles, and assignments | Completed and verified | Layered backend, JWT sessions, secure hashes, three roles, persisted Viewer assignments, login/logout events, protected React routes, 9 backend tests, 3 frontend tests, live verification script, HTML guide |
| 3. Camera management and health | Not started | — |
| 4. Command center dashboard | Not started | — |
| 5. Multi-camera live monitoring | Not started | — |
| 6. Real recording workflows | Not started | — |
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

- ASP.NET Core password hashing stores salted PBKDF2 hashes, never the seeded
  plaintext passwords.
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
- `dotnet test Vms.slnx`: 9 passed; frontend tests: 3 passed.
- Backend build completed with 0 warnings/0 errors; ESLint and the frontend
  production build passed.
- `scripts/verify-auth.ps1` passed against PostgreSQL and the running Compose
  stack; all eight services were healthy.
- Step 1 foundation verification passed again with four decodable HLS feeds.
- `docs/steps/step-02-authentication-roles-assignments.html` documents the
  architecture, dependencies, code, proof, tradeoffs, and Step 3 handoff.
