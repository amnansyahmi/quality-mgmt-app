# SMA Complaint Management

The solution contains an ASP.NET Core MVC application with SQL Server persistence and a separate workflow verification project. Open `SmaComplaints.sln` in Visual Studio or use the .NET 10 CLI.

To explore the application now, run `Start-Demo.ps1`, or follow the equivalent commands in `SmaComplaints/README.md`. Open http://127.0.0.1:5079. Choose a role to preview its inbox, forms, and approval tasks. Development uses clearly marked synthetic sample data; it resets when the process restarts.

The app includes complaint Parts A–C, assignment, saved drafts, returns for correction, reporting review, action planning and completion approvals, evidence uploads, delay and extension requests, effectiveness checks, and QM/CM closure. It also includes a CSV register, a printable report, people/role management, and a configurable SMTP outbox worker.

Use **New complaint** as a Requestor to start Part A. Administrators can use **Form Studio** to upload DOCX, PDF and scanned PNG/JPG forms, review proposed fields/tables and sequential workflows, then publish a named menu and record workspace. Local English OCR is included. Complex conditional rules and handwriting are not automatically interpreted. See `SmaComplaints/FORM-STUDIO.md` and `SmaComplaints/COMPLAINT-FIELD-MAP.md`. Existing SQL installations need `002_forms.sql`.

SQL Server connection settings remain blank and configurable as requested. Email delivery is disabled until configured. See `SmaComplaints/deployment/appsettings.Production.example.json` and the setup guide in `SmaComplaints/README.md` for the database schema, first administrator, SQL connection, SMTP, and IIS deployment instructions.

Verification on 27 October 2026:

- Release build and publish succeeded.
- 79 workflow, authorization, and concurrency checks passed.
- 257 HTTP checks passed across complaint navigation/workflows, DOCX/PDF/scanned imports, publishing, generated records, typed values, later-stage editing, evidence access, approvals and returns.
- Interactive Blazor dashboard tested: chart filters, queue filters, record inspection, and responsive layouts.
- SQL Server and SMTP integration remain untested because no live database or relay was connected. This is a working first version for review and deployment configuration, not a validated production QMS.

No real complaints, company user accounts, SQL credentials, SMTP credentials, or private authentication keys are included in this source bundle.

The current workspace revision adds role-aware menus, Show help, and in-app handoff/return notifications. Due & overdue is now reached from the register, and New complaint creates a draft directly for Requestors. SQL deployments also need 003_notifications.sql. See SmaComplaints/WORKSPACE-FLOW.md. Current verification: Release publish/app startup and 327 HTTP checks passed; the expanded standalone unit runner was blocked by Windows application-control policy. SQL/SMTP integration still needs configuration and verification.

Latest UI update: a 1-based workflow stepper, bell/help icons and the Form Studio preview fix are included. The current build starts successfully and passed 327 HTTP checks, plus desktop/mobile browser verification. The preview is available on port 5081 in the active session.
