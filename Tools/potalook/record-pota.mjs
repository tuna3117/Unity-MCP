// Records Pota's puzzle from the SCRATCH COPY (served by its vite) at 9:16 for the side-by-side with Unity. Never runs in ~/projects/Pota.
// Usage: node record-pota.mjs <url> <out dir> <play|win>  →  <out dir>/*.webm + <out dir>/events.json {tap, rotate, win} (s from video start)
import { chromium } from "/Users/tunaaltin17/projects/copy/node_modules/playwright/index.mjs";
import { writeFileSync } from "node:fs";
import { join } from "node:path";

const [url = "http://localhost:5199/", out, mode = "play"] = process.argv.slice(2);
if (!out) throw new Error("usage: node record-pota.mjs <url> <out dir> <play|win>");
const browser = await chromium.launch({ headless: false, args: ["--use-angle=metal", "--ignore-gpu-blocklist"] });
const ctx = await browser.newContext({
  viewport: { width: 390, height: 693 }, deviceScaleFactor: 2, hasTouch: true, locale: "tr-TR",
  recordVideo: { dir: out, size: { width: 390, height: 693 } },   // = the viewport: a larger size drops the page into the top-left corner
});
const t0 = Date.now();
const now = () => (Date.now() - t0) / 1000;
const page = await ctx.newPage();
const errors = [];
page.on("pageerror", (e) => errors.push(String(e)));
await page.goto(`${url}?test=renkavi`);
await page.waitForFunction(() => window.__pota?.faz2?.durum, null, { timeout: 20000 });

// A 7×7 level with no obstacle or special on the board: Pota's rules grew past Unity's, so moments are compared, not layouts.
let clean = false;
for (let tohum = 1; tohum <= 40 && !clean; tohum++) {
  await page.evaluate((t) => __pota.faz2.kur(9, 158, t, false, []), tohum);
  await page.waitForTimeout(400);
  clean = await page.evaluate(() => { const d = __pota.faz2.durum(); return d.boyut === 7 && d.engel === 0 && d.ozel === 0; });
}
if (!clean) throw new Error("no clean 7×7 board in 40 seeds");
// Win scenario: every target needs one more block (the copy's runtime state; the source stays untouched).
if (mode === "win") await page.evaluate(() => { for (const h of __pota.faz2.faz.alan.hedefler) h.kalan = 1; });

// Sync marker: a magenta 10 px square in the top-left corner for 250 ms at every event (compare.py aligns on its first frame).
await page.evaluate(() => {
  const m = document.createElement("div");
  m.id = "potalook-mark";
  m.style.cssText = "position:fixed;left:0;top:0;width:10px;height:10px;z-index:99999;pointer-events:none;background:transparent";
  document.body.appendChild(m);
  window.__isaret = () => { m.style.background = "#ff00ff"; setTimeout(() => { m.style.background = "transparent"; }, 250); };
});
const settle = () => page.waitForFunction(() => { const d = __pota.faz2.durum(); return !d.kilit && d.hudBekleyen === 0; }, null, { timeout: 15000 });
// Longest-chain affordable block like ai_colorhunt_record "auto"; win: a block of the first target's colour.
const pick = () => page.evaluate(async (win) => {
  const R = await import("/src/renkAvi.ts");
  const alan = __pota.faz2.alan(), tahta = __pota.faz2.tahta().filter((b) => b.yeter);
  if (win) { const renk = alan.hedefler[0].renk; return (tahta.find((b) => b.renk === renk) ?? tahta[0]).hucre; }
  let best = -1, bestSteps = -1, bestHp = 1e9;
  for (const b of tahta) {
    const k = R.kir(alan, b.hucre);
    const steps = k?.adimlar?.length ?? 0;
    if (steps > bestSteps || (steps === bestSteps && b.can < bestHp)) { best = b.hucre; bestSteps = steps; bestHp = b.can; }
  }
  return best;
}, mode === "win");

const events = {};
await page.waitForTimeout(900);
if (mode === "win") {
  const c = await pick();
  events.win = now();
  await page.evaluate((h) => { window.__isaret(); __pota.faz2.kir(h); }, c);
  await page.waitForTimeout(2800);
} else {
  let c = await pick();
  events.tap = now();
  await page.evaluate((h) => { window.__isaret(); __pota.faz2.kir(h); }, c);
  await settle();
  await page.waitForTimeout(400);
  events.rotate = now();
  await page.evaluate(() => { window.__isaret(); __pota.faz2.dondur("saat"); });
  await settle();
  await page.waitForTimeout(300);
  c = await pick();
  await page.evaluate((h) => __pota.faz2.kir(h), c);   // unmarked: only tap and rotate are anchors
  await settle();
  await page.waitForTimeout(1500);
}
await ctx.close();
await browser.close();
writeFileSync(join(out, "events.json"), JSON.stringify(events));
console.log(JSON.stringify({ events, errors }));
