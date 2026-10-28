# Demo deployment

This deploys the **demo build only**: the app runs in Development with synthetic in-memory records and six role shortcuts. Data resets whenever the process restarts. It never connects to SQL Server or SMTP. Do not enter real complaint data.

The demo keeps its records and cookie keys in process memory, so it needs a host that runs **one long-lived instance**. Render fits. Vercel does not (see below).

## Files

- `Dockerfile`: builds the app with the .NET 10 SDK and runs it on the ASP.NET runtime image (Debian, with tzdata for Asia/Kuala_Lumpur).
- `render.yaml`: Render Blueprint for a free Docker web service in Singapore, health-checked on `/Account/Login`.
- `Program.cs`: binds Kestrel to `PORT` when it is set (Render sets it), and lets `DataProtection:KeysPath` override the key-ring folder. Behaviour is unchanged when neither is set.

## Deploy on Render

1. In Render, choose **New > Blueprint** and connect `amnansyahmi/quality-mgmt-app`.
2. Select the branch that contains `render.yaml` (`vercel-demo` until it is merged to `main`).
3. Apply the Blueprint. Render builds the Dockerfile and starts one instance.
4. Open the service URL and pick a role.

Notes:
- A free instance sleeps after a period of inactivity. The first request afterwards is slow, and the demo data resets.
- Cookie keys also reset on a restart. If a form was open across a restart, reload the page.
- Confirm the current free-plan limits in Render's documentation before relying on it.

## Why not Vercel

A Vercel container deployment worked, but it cannot hold this demo. Vercel starts new stateless instances as traffic arrives, and requests can land on different ones. Each instance generates its own cookie keys, so the form token from one instance is rejected by the next (HTTP 400 on `/Account/Demo`). Records created in the demo would also disappear between requests. Vercel has no setting to pin a single instance. `vercel.json` is kept only so the existing Vercel project does not fall back to a blank page. Remove it if you no longer use Vercel for this repo.

## Known limits of the demo

- **Public access:** anyone with the URL can pick any role, including Administrator. Do not share the link widely.
- **Scanned-form OCR:** the Tesseract native libraries are not installed. DOCX and text-PDF imports work. Scanned PDF and image imports show the "could not be read" message.
- **Memory:** the free instance has limited RAM. Large PDF or scan imports may fail.
- **Redeploys:** every deploy restarts the process and resets the data.

## Production

Use a host with SQL Server connectivity and persistent storage, with `DemoMode=false`. See `SmaComplaints/README.md`.
