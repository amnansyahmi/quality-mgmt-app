# Quality Management App

Standalone complaint and form workflow system built with ASP.NET Core MVC (.NET 10), SQL Server, and a Blazor dashboard.

## Included

- Complaint handling Parts A–C with evidence, reporting review, named approvals, returns and audit history.
- Role-aware menus, task inbox, role help and in-app handoff notifications.
- Form Studio imports DOCX, PDF and scanned forms with local English OCR; administrators review fields and sequential workflows before publishing.
- Numbered workflow progress, repeatable rows, and a Chart.js dashboard.
- SQL migrations, configurable SMTP outbox and workflow checks.

## Run locally

Install the .NET 10 SDK, then run `./Start-Demo.ps1` in PowerShell, or:

```powershell
dotnet restore SmaComplaints.sln
dotnet build SmaComplaints.sln -p:UseAppHost=false
dotnet ./SmaComplaints/bin/Debug/net10.0/SmaComplaints.dll --environment Development --urls http://127.0.0.1:5079
```

Development uses synthetic in-memory records and demo role shortcuts; records reset on restart. Production uses SQL Server and normal account authentication.

## Guides

- [Setup, roles and deployment](SmaComplaints/README.md)
- [Form Studio capabilities and limits](SmaComplaints/FORM-STUDIO.md)
- [Navigation and notifications](SmaComplaints/WORKSPACE-FLOW.md)
- [Complaint field mapping](SmaComplaints/COMPLAINT-FIELD-MAP.md)
- [Third-party licenses](SmaComplaints/THIRD-PARTY.md)

SQL connection and SMTP settings are configurable. Apply migrations 001–003 before production use; see the deployment guide. Local data-protection keys, uploaded forms/evidence and credentials are excluded. Live SQL Server and SMTP integration still require deployment verification.

## Workflow checks

```powershell
dotnet run --project SmaComplaints.Checks
```
