// Captures screenshots and rendered HTML of the demo app at phone and desktop sizes.
// Usage: node shots.mjs <baseUrl> <outDir>
import { chromium, devices } from "playwright";
import fs from "node:fs/promises";
import path from "node:path";

const base = process.argv[2] ?? "http://127.0.0.1:5079";
const out = process.argv[3] ?? "shots";
await fs.mkdir(path.join(out, "html"), { recursive: true });

const viewports = {
  phone: { ...devices["iPhone 13"], viewport: { width: 390, height: 844 } },
  tablet: { viewport: { width: 820, height: 1180 }, deviceScaleFactor: 1, hasTouch: true },
  desktop: { viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 },
};
// Tablet only needs the shell and a dense page.
const tabletPages = new Set(["01-login", "03-inbox-requestor", "04-register", "06-details"]);

const report = [];
const browser = await chromium.launch();

async function capture(page, vp, name, { full = true, saveHtml = false } = {}) {
  if (vp === "tablet" && !tabletPages.has(name)) return;
  await page.waitForLoadState("networkidle").catch(() => {});
  await page.waitForTimeout(400);
  const file = `${vp}-${name}.png`;
  await page.screenshot({ path: path.join(out, file), fullPage: full });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  report.push(`${file}  url=${page.url().replace(base, "")}  horizontalOverflow=${overflow}px`);
  if (saveHtml) await fs.writeFile(path.join(out, "html", `${vp}-${name}.html`), await page.content());
}

async function signIn(page, role) {
  await page.goto(`${base}/Account/Login`);
  await page.locator(`form[action="/Account/Demo"] input[name="role"][value="${role}"]`).locator("xpath=..").locator("button").click();
  await page.waitForLoadState("networkidle");
}

for (const [vp, opts] of Object.entries(viewports)) {
  const errors = [];
  const ctx = await browser.newContext(opts);
  const page = await ctx.newPage();
  page.on("console", (m) => m.type() === "error" && errors.push(`console: ${m.text()}`));
  page.on("response", (r) => r.status() >= 400 && errors.push(`${r.status()} ${r.url().replace(base, "")}`));

  await page.goto(`${base}/Account/Login`);
  await capture(page, vp, "01-login", { saveHtml: true });

  await signIn(page, "Requestor");
  await capture(page, vp, "02-overview-requestor", { saveHtml: true });
  const menu = page.locator("[data-drawer-open]");
  if (vp === "phone" && (await menu.isVisible().catch(() => false))) {
    await menu.click();
    await page.waitForTimeout(350);
    await capture(page, vp, "02b-menu-drawer", { full: false });
    await page.keyboard.press("Escape");
    await page.waitForTimeout(300);
  }
  await page.goto(`${base}/Complaints?view=inbox`);
  await capture(page, vp, "03-inbox-requestor", { saveHtml: true });
  await page.goto(`${base}/Complaints?view=register`);
  await capture(page, vp, "04-register", { saveHtml: true });
  const detail = await page.locator("a.queue-reference").first().getAttribute("href").catch(() => null);
  await page.goto(`${base}/Complaints?view=drafts`);
  await capture(page, vp, "05-drafts", { saveHtml: true });
  if (detail) {
    await page.goto(`${base}${detail}`);
    await capture(page, vp, "06-details", { saveHtml: true });
  }
  await page.goto(`${base}/Forms`);
  await capture(page, vp, "07-forms", { saveHtml: true });

  await signIn(page, "QM");
  await page.goto(`${base}/Complaints?view=inbox`);
  await capture(page, vp, "08-inbox-qm", { saveHtml: true });
  const qmTask = await page.locator("a[href*='/Complaints/Details/']").first().getAttribute("href").catch(() => null);
  if (qmTask) {
    await page.goto(`${base}${qmTask}`);
    await capture(page, vp, "09-details-qm", { saveHtml: true });
  }
  const help = page.locator("[data-open-dialog='role-help']").first();
  if (await help.isVisible().catch(() => false)) {
    await help.click();
    await capture(page, vp, "10-help-dialog", { full: false });
    await page.keyboard.press("Escape");
  }
  const bell = page.locator("#notification-bell");
  if (await bell.isVisible().catch(() => false)) {
    await bell.click();
    await page.waitForTimeout(600);
    await capture(page, vp, "11-notifications", { full: false });
    await page.keyboard.press("Escape");
  }

  await signIn(page, "Admin");
  await page.goto(`${base}/Admin`);
  await capture(page, vp, "12-admin", { saveHtml: true });
  await page.goto(`${base}/FormStudio`);
  await capture(page, vp, "13-form-studio", { saveHtml: true });

  report.push(`--- ${vp} errors ---`, ...(errors.length ? errors : ["none"]));
  await ctx.close();
}

await browser.close();
await fs.writeFile(path.join(out, "report.txt"), report.join("\n") + "\n");
console.log(report.join("\n"));
