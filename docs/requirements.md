# CivicConnect initial requirements

These initial user stories translate the project proposal into a buildable prototype. Confirm the pilot area, participating authority, terminology, and workflow with residents, officers, and the supervisor during requirements gathering. The stories do not claim that every authority currently uses the same reporting process.

## Citizen

- As a resident, I can create an account and sign in so I can submit and follow my own reports.
- As a resident, I can choose a configured pilot area and issue category, describe an issue, add a photo, and pin its location on a map.
- As a resident, I receive a reference number after submitting a valid report.
- As a resident, I can see my report status and its status history, while other residents cannot see my report details.
- As a resident, I receive an in-app notification when an officer assigns or updates my report.

## Officer

- As an officer, I can sign in to the account created for me by an administrator.
- As an officer, I can see unassigned reports and reports for my department, and take a report for my department.
- As the assigned officer, I can update a report status and add a note for the resident.
- As an officer, I can see the report location and an attached photo when I am authorized to review that report.

## Administrator

- As an administrator, I can configure pilot areas and districts, departments, issue categories, and officer accounts.
- As an administrator, I can assign a report to an officer in the selected department.
- As an administrator, I can update report status and review reports in the pilot area.

## Prototype boundaries

- A report requires a pilot area, category, description, and latitude/longitude. A photo is optional and limited to JPG, PNG, or WebP files up to 5 MB.
- Citizen accounts can access only their own report details and photos. Officer and administrator access is controlled by role and assignment/department.
- The prototype records in-app notifications and status history. It does not send email or SMS, integrate with government systems, or verify physical repair.
- The pilot authority and area are configuration choices; identify them during requirements gathering before presenting the workflow as an operational service.
