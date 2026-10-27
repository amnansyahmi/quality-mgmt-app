# SMA Complaint Management

A standalone ASP.NET Core MVC application using SQL Server, based on SMA-FM-QM-05 Rev 07 dated 12 September 2025. The supplied IFE Joget training deck informed the inbox, draft, return, approval, and tracking patterns. Its factory-specific roles, testing rules, three-evaluation rejection limit, and three-day reminder policy were not adopted as complaint-handling rules.

## Included

- MVC controllers and server-rendered Razor views on .NET 10.
- SQL Server persistence through Microsoft.Data.SqlClient 6.1.4 and parameterized commands.
- Requestor, Complaint Owner, HOD, QM, CM, and Admin roles, with server-side record and task authorization.
- Parts A, B, and C, including repeatable action rows, immediate corrections, reporting criteria, investigation, two rounds of action approval, effectiveness checks, and closure.
- Saved drafts, returns for correction under the same reference, version checks to reject stale writes, and an activity and approval history.
- Evidence uploads and authorized downloads, stored in SQL Server. Up to 10 MB per file, 20 files / 50 MB per record.
- Search, stage filters, task inboxes, due/overdue views, CSV register export, and a printable full report. The browser's print dialog can save the report as PDF.
- Transactional SQL email outbox, stage notifications, retries, and a daily overdue reminder per recipient and complaint when SMTP delivery is enabled.
- Admin screens to add users, assign roles, reset passwords, and deactivate accounts. Production has no default accounts or demo sign-in shortcuts.

## Run the development preview

Install the .NET 10 SDK, then open a terminal in this project folder:

```powershell
dotnet restore
dotnet build
dotnet run --no-launch-profile -- --environment Development --urls http://127.0.0.1:5079
```

Open http://127.0.0.1:5079. Development configuration enables an **explicitly marked demo** with synthetic sample records and six role shortcuts. Its in-memory data resets on restart. It never connects to SQL Server or sends email. For a machine where apphost execution is restricted, the equivalent standard runtime command after building is:

```powershell
dotnet .\bin\Debug\net10.0\SmaComplaints.dll --environment Development --urls http://127.0.0.1:5079
```

## Connect SQL Server

Connection settings are intentionally configurable. No company server, database, account, password, or SMTP service has been assumed or connected.

1. Create an empty SQL Server database named `SmaComplaints` (or choose another name in the connection string). SQL Server 2019 or later is the deployment target.
2. Copy the structure of `deployment/appsettings.Production.example.json` into your deployment configuration. Set `DemoMode=false` and your hostname. Supply the actual connection string through environment variables or your host's secret configuration. Do not commit credentials.
3. Set the runtime environment and connection. For Windows integrated authentication, the app's Windows identity must have database access. For SQL authentication, obtain credentials from your administrator and provide them through the host's secret store.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:DemoMode = 'false'
$env:ConnectionStrings__Complaints = 'Server=YOUR_SERVER;Database=SmaComplaints;Integrated Security=True;Encrypt=True;TrustServerCertificate=False'
dotnet run --no-launch-profile -- --initialize-database
```

The initialization command runs `deployment/001_schema.sql`. It creates the user, complaint, append-only application audit, attachment, and mail outbox tables. It does not create the database. It is idempotent for these initial tables and does not drop existing data. Use a database deployment identity for initialization; the runtime identity should have only the required read/write permissions. Restrict application audit-table privileges to SELECT and INSERT for the runtime identity. Back up the database, which also holds uploaded evidence.

For SQL-backed local development, keep `ASPNETCORE_ENVIRONMENT=Development` and explicitly set `DemoMode=false`. A development-only example for a trusted local instance is:

```powershell
$env:DemoMode = 'false'
$env:ConnectionStrings__Complaints = 'Server=(localdb)\MSSQLLocalDB;Database=SmaComplaints;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
```

Certificate trust in that example is limited to local development. Keep certificate validation enabled on the deployed server.

## Create the first administrator

Run this once against an empty user table. Choose a password of at least 12 characters. Use a secure environment injection mechanism for `SMA_ADMIN_PASSWORD`; do not put the password in command arguments or source files.

```powershell
$env:SMA_ADMIN_EMAIL = 'your-admin@your-company.example'
$env:SMA_ADMIN_NAME = 'Your Administrator'
# Set SMA_ADMIN_PASSWORD securely in this process environment.
dotnet run --no-launch-profile -- --bootstrap-admin
Remove-Item Env:SMA_ADMIN_PASSWORD
```

Sign in as that administrator and create the Requestor, Owner, HOD, QM, and CM accounts in **People & roles**. Assign real people during evaluation. The app requires an active user with the matching role for every assignment. Disabling a user, changing their role, or resetting their password invalidates their existing session on its next request.

## Workflow

| Stage | Responsible person | Result |
|---|---|---|
| Part A draft | Requestor | Save details, upload evidence, submit |
| Evaluation | QM | Return notification or assign department, owner, and approvers |
| Investigation | Assigned Owner | Evaluate, investigate root cause, prepare action plan |
| Reporting review during investigation | Assigned QM | Confirm reportability and manually set reporting due date |
| Action plan approval | Assigned HOD, then QM | Approve or return to Owner |
| Action completion | Assigned Owner | Record completion dates and objective evidence |
| Completed actions approval | Assigned HOD, then QM | Approve or return to Owner |
| Effectiveness | Assigned QM | Record monitoring checks and evidence |
| Closure | Assigned QM, then CM | Review and approve closure, or return to effectiveness |

The application follows the supplied form's rule that any Yes in its four quality-complaint criteria requires reporting. QM confirms that review before the action plan can advance. A change to the category or criteria invalidates the previous review. Reporting due dates are deliberately entered by QM. The app does not file a regulatory report or calculate statutory reporting deadlines.

Urgency deadlines use calendar time from record creation: 24 hours, 7 days, or one calendar month. Evaluation initially receives a 24-hour task deadline as an implementation default. The Owner may request an extension, which HOD must approve; an approved extension survives subsequent plan approval. Responses exceeding seven days require an approved HOD delay reason before completed actions can advance. Malaysia time (UTC+8) is used for display, date-based deadlines end at 17:00 MYT, and event timestamps are stored as offsets. Confirm these business defaults with your process owner before operational rollout.

Approved action-plan descriptions, responsible people, and planned dates are locked during action completion. Returns move the record to the relevant editable stage and retain its reference and history. There is no automatic rejection after a fixed number of returns. Closure locks the record. Approvals record an authenticated account, role, decision, and time; this implementation does not supply certificate-based digital signatures or claim validated QMS compliance.

## Configure email notifications

Email is disabled by default. SQL-backed workflow events still enter `dbo.MailOutbox`, so decide whether to retain or clear old queued test notifications before enabling real delivery. Demo mode never starts the mail worker.

Configure these environment settings on the server:

```text
Mail__Enabled=true
Mail__Host=your-smtp-host
Mail__Port=587
Mail__From=complaints@your-company.example
Mail__Username=<if required by your relay>
Mail__Password=<secret supplied by your host>
Mail__BaseUrl=https://your-deployed-app-hostname
```

The worker runs once per minute while the web process is running. It queues at most one overdue reminder per complaint/recipient/Malaysia date, claims work with a five-minute lease, retries a failed send after 15 minutes, and stops retrying after 20 attempts. Inspect unsent rows and `LastError` in `dbo.MailOutbox` and application logs. Delivery is at least once: a crash after SMTP accepts a message but before SQL records delivery can cause a duplicate. No SMTP server has been contacted during development checks.

On IIS, configure the app pool to remain running (AlwaysRunning, preload, and appropriate idle settings), or extract the worker to a dedicated .NET Worker Service if reminders must operate independently of web traffic. Set the hostname and HTTPS before enabling notification links.

## Build and deploy

```powershell
dotnet publish -c Release -o .\publish
```

Deploy the published output to IIS with the .NET 10 Hosting Bundle or another .NET-compatible host. Configure HTTPS, production environment, hostnames, SQL credentials, and optional SMTP settings. Keep `App_Data/Keys` persistent across restarts and backed up; restrict it to the app identity. In a server farm use a shared protected Data Protection key ring. Confirm upload scanning, retention, backup, and authentication policies with your IT/QM team before using real complaint data.

## Checks and verification limits

The sibling `SmaComplaints.Checks` project has no test-framework dependency:

```powershell
dotnet build ..\SmaComplaints.Checks\SmaComplaints.Checks.csproj
dotnet ..\SmaComplaints.Checks\bin\Debug\net10.0\SmaComplaints.Checks.dll
```

Development verification covered workflow gates, role assignment, draft privacy, return routing, required evidence, future completion dates, extensions, stale revisions, and the full HTTP path from creation through CM closure. SQL Server LocalDB could not be accessed in the authoring session, so the SQL adapter and schema were compiled and reviewed but have not been integration-tested against a live database. SMTP delivery also remains untested until configured. The current SQL register returns the latest 500 accessible records; add database paging and server-side reporting before high-volume use. PDF output uses browser printing, and the report preserves form content rather than reproducing the Word layout pixel for pixel.

## Technical references

- [Microsoft.Data.SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [ASP.NET Core cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

## Dashboard widgets

The Blazor dashboard uses locally bundled Chart.js 4.6.1 (MIT). The license is included at `wwwroot/vendor/chart.js/LICENSE.md`. Node is needed only when rebuilding frontend assets: run `npm ci` and `npm run build`. Sidebar selection uses a neutral fill without a colored side stripe.

## Work screens and approval handoffs

- Overview: Chart.js analytics and an operational summary.
- My task inbox: assigned forms, approval decisions, QM reportability review, and HOD exception requests.
- Complaint register: searchable accessible records and their current task holders.
- Drafts & resubmissions: only your own unsubmitted or returned notifications.
- Due & overdue: overdue records and records due within three days.

On an approval task, select the next active recipient in the required role and choose Approve & forward. The task leaves your inbox and appears in the recipient’s inbox. Return for correction requires a reason and permits the preparer or an allowed previous reviewer. The complaint keeps its reference and evidence; after correction, resubmission repeats the mandatory approval sequence. Every handoff/return is recorded with the actor, stage change and recipient. Existing SQL aggregate storage needs no schema change for these features.

This revision passed 79 workflow/authorization checks and 257 HTTP checks using synthetic in-memory data. Coverage includes six-role navigation, named handoffs, returns/resubmission, stale decisions, DOCX/text PDF/scanned PDF/image OCR imports, reviewed publishing, stage-specific field validation and evidence access. SQL Server and SMTP integration remain unverified until configured.

## Form Studio and complaint creation

Use the persistent New complaint menu. Requestors can start Part A; other roles see an explanation and a development-only switch to Requestor. See COMPLAINT-FIELD-MAP.md for the source mapping and automatic identity/timestamp treatment. Handwritten signature images are not collected.

Administrators can import DOCX, PDF and scanned PNG/JPG forms in Form Studio, review fields/tables and sequential role workflows, and publish named modules. Records route through the shared inbox with approvals, returns, attachments and history. See FORM-STUDIO.md for limits, native OCR setup and the required 002_forms.sql migration. This is a local proposal generator with human review; conditional business rules are not automatically implemented.

## Workspace navigation, help and notifications

Show help explains all six roles, permissions, the complaint sequence and generated form tasks. Notifications provides unread counts, direct record links and recipient-specific read status. Handoffs, returns, reportability reviews, exceptions and closure create in-app notifications. See WORKSPACE-FLOW.md for the menu review, delivery behavior and limits. SQL deployments must apply 003_notifications.sql before this revision.

Current build: Release publish and app startup succeeded, with 327 HTTP checks passing (198 complaint workflow/navigation, 41 generated forms, 48 notification/menu, 18 imports and 22 preview regressions). The current preview runs this build, including the inbox counter/search refinements. Form Studio preview uses an explicit shared-partial path. The numbered stepper and icon-only header were checked in the browser at desktop and mobile widths. The separate expanded unit runner was previously blocked by Windows application-control policy; no security settings were changed. Live SQL Server and SMTP remain unverified.
