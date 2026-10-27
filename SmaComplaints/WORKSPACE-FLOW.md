# Workspace flow review

The primary work entry is My task inbox. Overview remains complaint analytics, the register is the searchable complaint list, and Due & overdue is a register shortcut rather than another permanent menu item. Requestors also have their private Drafts & resubmissions. The New complaint menu button creates a draft directly; the old /Complaints/New guide remains available.

Published modules stay under Forms, with Form Studio and People & roles in a separate administrator group. Non-requestors no longer see creation/draft menu entries they cannot use. Existing bookmarked routes are retained. Unused ASP.NET welcome/privacy views and their unused layout/site assets were removed; /Home and /Home/Privacy redirect to the workspace.

Complaint records without an explicit tab open at the current task section. Inbox links preserve a return-to-inbox breadcrumb. The duplicate Next-stage summary was removed because the same sequence is already visible in the workflow strip. Required preparation, evidence, reporting gates and approval routes remain in place.

Show help is available on every signed-in screen. It explains each role, limits, the complaint sequence, generated form permissions and notification behavior.

## In-app notifications

The Notifications button shows unread count and a drawer with the latest 50 notifications (unread first), record links, mark read and mark all read. Opening a notification marks only that recipient's copy read. Reading does not complete a task. The badge polls every 30 seconds while the page is visible and refreshes on opening the drawer. All notifications remain available in SQL; only the latest 50 are shown. There are no browser push notifications.

Recipients receive notifications for complaint assignment/forwarding/returns, HOD delay and extension requests and decisions, reporting-ready/completed reviews, final closure, and generated-form forwards/returns/completion. Unassigned complaint evaluation notifies active QM accounts. Saves and evidence uploads do not emit routine notifications. Existing tasks remain in My task inbox; notifications begin with new workflow events after this change and are not backfilled.

SQL notifications commit in the same transaction as complaint audit or generated-record updates and uploads. Event/recipient keys prevent duplicate deliveries; stale writes cannot create notifications. Demo notifications are held in memory and reset on restart.

Run deployment/003_notifications.sql (or --initialize-database with DemoMode disabled) before deploying. Missing notification schema prevents workflow writes that need notifications, rather than silently losing delivery. SQL Server integration still needs live verification. Complaint SMTP outbox delivery requires configured/enabled Mail settings. Generated-form notifications are in-app only.

## Workflow presentation and verification

Generated records use a 1-based numbered stepper with connecting arrows, completed checkmarks, a current step and upcoming steps. Returned records identify the step needing correction and reset later reviews. Stored workflow indexes remain unchanged. On narrow screens, the stepper scrolls horizontally and brings the current step into view. The header uses a bell and question-mark help icon with accessible names, tooltips and unread count.

Form Studio Preview resolves ~/Views/Forms/_Fields.cshtml explicitly so it works from both Forms and FormStudio controller contexts. Preview remains read only and cannot submit a record. DOCX, text PDF, image-only PDF and image scans were checked, along with the original complaint form.

The current build publishes, starts, and passes 327 HTTP checks. Browser checks cover current, completed, returned and preview states, icon actions and mobile width. Earlier preview-start blocks are resolved for this build. The standalone expanded unit runner was previously blocked by Windows application-control policy; SQL Server and SMTP remain unverified.
