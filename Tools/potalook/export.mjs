// Opens the export page (served by the Pota scratch copy's vite) in headless Chromium and writes window.__disa
// (file name → base64) into <out dir>. Usage: node export.mjs <url> <out dir>
import { chromium } from "/Users/tunaaltin17/projects/copy/node_modules/playwright/index.mjs";
import { mkdirSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const [url, out] = process.argv.slice(2);
if (!url || !out) throw new Error("usage: node export.mjs <url> <out dir>");
mkdirSync(out, { recursive: true });
const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  page.on("pageerror", (e) => console.error("[page]", e.message));
  page.on("console", (m) => { if (m.type() === "error") console.error("[console]", m.text()); });
  let open = false;
  for (let i = 0; i < 60 && !open; i++) {       // vite may still be starting
    try { await page.goto(url, { waitUntil: "load", timeout: 5000 }); open = true; }
    catch { await new Promise((r) => setTimeout(r, 500)); }
  }
  if (!open) throw new Error(`page did not open: ${url}`);
  await page.waitForFunction(() => window.__disa !== undefined, null, { timeout: 30000 });
  const files = await page.evaluate(() => window.__disa);
  for (const [name, b64] of Object.entries(files)) {
    const bytes = Buffer.from(b64, "base64");
    writeFileSync(join(out, name), bytes);
    console.log(`${name}\t${bytes.length}`);
  }
} finally {
  await browser.close();
}
