# Vercel demo deployment

This deploys the **demo build only**: the app runs in Development with synthetic in-memory records and the six role shortcuts. Data resets whenever an instance restarts. It never connects to SQL Server or SMTP. Do not enter real complaint data.

## Files

- `Dockerfile.vercel`: builds the app with the .NET 10 SDK and runs it on the ASP.NET runtime image (Debian, with tzdata for Asia/Kuala_Lumpur).
- `vercel.json`: runs the Dockerfile as a container service and routes every path to it.
- `Program.cs`: binds Kestrel to `PORT` when it is set, and lets `DataProtection:KeysPath` override the key-ring folder. Behaviour is unchanged when neither is set.

## Deploy

Push to `main`. The existing Vercel project builds from the repo root. Or from the CLI: `vercel deploy --prod`.

If the deployment returns 502, set `PORT=8080` under Project Settings > Environment Variables and redeploy. This is needed when the container cannot bind port 80.

## Known limits of the demo

- **Container runtime availability:** confirm in the Vercel dashboard that container deployments work on your plan.
- **Instances and sessions:** each instance has its own in-memory data and its own cookie keys (stored in `/tmp`). If a request lands on a different instance, you can be signed out or see different data.
- **Dashboard (Blazor Server):** it needs a persistent connection (WebSocket, or a long-polling fallback). If it renders but its charts or interactions do not work, the cause is Vercel's function model, not the app.
- **Uploads:** Vercel limits request bodies to 4.5 MB, so evidence and Form Studio uploads larger than that return 413. The app's own limits are 10 MB and 20 MB.
- **Scanned-form OCR:** the Tesseract native libraries are not installed. DOCX and text-PDF imports work. Scanned PDF and image imports show the "could not be read" message.
- **Public access:** anyone with the URL can pick any role, including Administrator. Use Vercel Deployment Protection if the link should not be public.

## Production

Use a host that supports SQL Server connectivity and persistent storage, with `DemoMode=false`. See `SmaComplaints/README.md`.
