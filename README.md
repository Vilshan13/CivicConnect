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

## Run locally

Requirements: .NET 10 SDK, Node.js 20.19+ (or 22.12+), and Docker Compose.

1. Start PostgreSQL with `docker compose up -d db`.
2. Start the API from `server/CivicConnect.Api` with `dotnet run`.
3. In another terminal, start the web app from `client` with `npm install` and `npm run dev`.
4. Open the local address printed by Vite. The API uses `http://localhost:5080` by default.

The current implementation is the first report submission and listing slice. Authentication, officer assignment, status history, administration, and notifications are planned follow-up milestones. Do not deploy this prototype publicly until those access controls and production settings are in place.
