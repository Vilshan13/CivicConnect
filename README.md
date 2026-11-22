# CivicConnect

CivicConnect is a prototype for reporting and tracking community issues in a selected Colombo District pilot area. Residents can submit location-tagged reports with photos, and authorized officers can review reports, assign them, and record status updates.

## Project scope

The individual project will focus on one pilot area. It will include citizen accounts, issue reports, photo evidence, GPS or manually selected locations, reference numbers, status history, officer assignment and updates, basic administration, and in-app notifications. It will not integrate with government systems or promise that a reported issue will be physically resolved. Requirements for the pilot area and its authority will be checked during requirements gathering.

## Proposed technology

- Frontend: React and JavaScript
- API: C# and ASP.NET Core Web API
- Database: PostgreSQL with Entity Framework Core
- Maps: Leaflet and OpenStreetMap

## Build milestones

1. Repository foundation and agreed project scope
2. Requirements, user roles, and report workflow
3. Backend API and database model
4. Citizen registration and login
5. Report submission, photo upload, and location selection
6. Officer dashboard, assignment, and status history
7. Administration and in-app notifications
8. Integration, security, and usability evaluation

Each milestone will be implemented and reviewed as a small change, then saved in a Git commit. The implementation details may be refined with supervisor approval as stated in the proposal.

The initial user stories and prototype boundaries are in [docs/requirements.md](docs/requirements.md). Confirm them with the selected pilot stakeholders before treating them as final requirements.

## Run locally

Requirements: .NET 10 SDK, Node.js 20.19+ (or 22.12+), and Docker Compose.

1. Start PostgreSQL with `docker compose up -d db`.
2. Before the first API start, set `SeedAdmin__Email` and `SeedAdmin__Password` in your local shell. Use your own private email and a strong password; never commit these values.
3. Start the API from `server/CivicConnect.Api` with `dotnet run`.
4. In another terminal, start the web app from `client` with `npm install` and `npm run dev`.
5. Open the local address printed by Vite. The API uses `http://localhost:5080` by default.

Citizens can register in the app. The configured administrator account is created at first API startup; use it to add pilot departments and officer accounts. Officers can review reports, take work for their department, update status, and add notes. Citizens see only their own reports and status history, with in-app notifications when reports change.

The administrator must first add the selected pilot area in Administration, then add the participating department and officer accounts. Reports store both their pilot area/district and map coordinates. Pilot area, department, and category management is available in the administrator screen.

## Implemented in this prototype

- Citizen registration/sign-in and role-protected citizen, officer, and administrator API actions
- Location-tagged reports with optional validated photo uploads, unique references, and private report/photo access
- Officer assignment, status updates with notes, citizen-visible status history, and in-app notifications
- Administrator setup for pilot areas, departments, categories, and officer accounts
- EF Core migrations applied at API startup

The Compose database credentials and development signing key are for local development only. Before any deployment, configure private database credentials and `Jwt__SigningKey` through a secret manager, use HTTPS, and review storage, backup, privacy, and pilot-authority requirements. The prototype does not connect to government systems or guarantee physical issue resolution.
