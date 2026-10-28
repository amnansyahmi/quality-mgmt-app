# Workflow console design

The requested UI/UX Pro Max and frontend-design skills were loaded from their upstream repositories for this task. Neither was installed globally. UI/UX Pro Max searches verified the Data-Dense Dashboard and Bento Box Grid entries. Its `html-tailwind` preset supplied focus guidance because there is no upstream `blazor-tailwind` preset. The user-requested density overrides the catalogue's spacious bento radii and hover scaling. Its unrelated marketing landing-page recommendation was discarded.

The token framework was created first in `ui-design-system.json`. The frontend-design pass then used those tokens to build `Components/WorkflowDashboard.razor`. `scripts/build-tokens.mjs` reads the JSON to generate `Styles/tokens.css`, which the Tailwind build incorporates into the shipped `wwwroot/css/workflow.css`. The compiled CSS is included so the .NET app can run without Node. To update frontend styles:

```powershell
npm ci
npm run build:css
```

The dashboard is an interactive Blazor Server component inside the existing MVC application. Controllers, authentication, mutation authorization, approvals, database access, and SQL configuration remain in MVC. The dashboard receives a projection containing only accessible records and display fields, without user password hashes. Filters, sorting, chart selection, theme preview, and the record inspector run in the Blazor circuit. Actual record updates continue through the existing authorized MVC routes.

The composition uses a single operational summary strip and a varied bento grid, with a work queue and contextual inspector. Widgets include stage distribution, a deadline-health donut, inactivity aging, department workload, a seven-day intake bar chart, and recent record activity. Values come from the accessible complaint records; there are no invented performance deltas or uptime figures. Current repository limits still apply to analytics: the SQL adapter reads the latest 500 accessible records, and the component displays at most 100 filtered queue rows.

Active, Paused, and Failed each have default, hover, selected, focus-visible, loading, disabled, error, and reduced-motion specifications in the JSON. The on-screen pipeline-state panel is explicitly a component reference. There is no connected pipeline execution engine, and overdue complaints are not relabelled as execution failures. Complaint workflow stage, deadline health, and runtime state are separate concepts.

The palette uses slate, white, and marine green, with amber and red reserved for attention states. Typography uses the locally available Aptos/Segoe UI family with tabular figures and a 12px minimum for meaningful desktop text. Controls and panel spacing are compact. There are no decorative purple gradients, hover-scale effects, or repeated oversized metric cards.

Upstream sources:

- [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill)
- [frontend-design](https://github.com/anthropics/skills/tree/main/skills/frontend-design)
- [Blazor and MVC integration](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/integration?view=aspnetcore-10.0)
- [Tailwind CLI](https://tailwindcss.com/docs/installation/tailwind-cli)

## Dashboard refinement · 27 Oct 2026

Applied the Impeccable distill guidance from the upstream skill, read task-locally. Its context launcher was unavailable, so the existing design context was read directly as instructed by the skill fallback. No automated Impeccable detector or multi-agent critique is claimed.

The established sidebar and navigation icons remain. Active navigation uses a neutral fill and label weight with no colored side stripe, inset rail, or side pseudo-element. The same shell appears on authenticated complaint pages. Widget outlines were replaced by a shared chart surface, alignment, and horizontal section rules. Pipeline state examples are collapsed until requested.

The five analytics widgets now use Chart.js 4.6.1, locally bundled with its MIT license. There is no CDN dependency. Data still comes from accessible complaint records. Charts use integer ticks, responsive canvases, tooltips, labelled text alternatives, and no animation. Stage bars and the first two deadline segments filter the queue through Blazor JS interop; keyboard users have equivalent stage and deadline buttons. Theme changes rebuild chart colors, and component disposal destroys chart instances. Daily intake uses bars to represent discrete counts.

Run `npm ci` then `npm run build` to rebuild both vendored Chart.js and CSS. Compiled assets are shipped for .NET-only startup.

- [Impeccable skill](https://github.com/pbakaus/impeccable/blob/main/.agents/skills/impeccable/SKILL.md)
- [Chart.js](https://www.chartjs.org/)
- [Chart.js MIT license](https://github.com/chartjs/Chart.js/blob/master/LICENSE.md)

## Purpose-specific work screens and handoffs

Overview is the only analytics dashboard. My task inbox lists actual preparation, approval, reportability, and exception tasks; the register provides record search; drafts contains the signed-in creator’s unsubmitted/returned notifications; deadlines separates overdue work from the next three days. All projections use the server-authorized record set.

The detail page shows the current phase with named task holders and a prominent handoff panel. Approve & forward validates an active recipient in the mandatory next role and routes the task to that person’s inbox. Return requires a reason and an allowed destination: the preparer or the immediately preceding reviewer in that approval phase. Returns preserve the reference, evidence, and history. Resubmission restarts the required approvals. Stale revisions and unauthorized decisions are rejected. Current return instructions persist through progress saves and clear when resubmitted; historical decisions remain recorded.

## Interface refinement · 28 Oct 2026

- **Icons.** One Lucide-based SVG set (ISC) replaces text glyphs and letter tiles: arrows, the ↗ that iOS draws as an emoji, box characters, fullwidth plus signs and the R/O/H/Q/C/A role tiles. Views use `<icon name="..." />` (`Services/IconTagHelper.cs`); the Blazor dashboard uses the same set through `ConsoleIcon`. Add icons in `scripts/build-icons.mjs` and run `npm run build:icons`. `SmaComplaints.Checks` fails if a view names an icon that does not exist. Arrows inside sentences (for example "HOD → QM") stay as text.
- **Type.** Inter (OFL) is self-hosted in `wwwroot/vendor/inter`, so phones no longer fall back to a different face. Text below 12px was raised (8–9px uppercase labels to 11px, 10–11px to 12px); body and controls are 14px.
- **Shell.** The wordmark replaces the letter-tile logo. The topbar is sticky. From 761px to 1000px the sidebar is an icon rail. At 760px and below a bottom tab bar (Overview, Inbox, Register, Forms, Menu) replaces the rail, and Menu opens the full navigation as a drawer with the signed-in account and sign-out. Safe-area insets are respected.
- **Phone content.** Register and people tables stack into labelled rows instead of scrolling sideways. Dialogs open as bottom sheets.
- **Zoom.** Fields are 16px on touch screens so iOS does not zoom on focus, and controls use `touch-action: manipulation`. Pinch zoom is not disabled.
- **Styles.** `wwwroot/css/ui.css` is loaded last and is readable source, organised by section.
- **Verification.** `.github/workflows/ui-preview.yml` builds the app, runs the checks and captures phone, tablet and desktop screenshots to the `ui-shots` branch.
