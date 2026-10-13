// Golden trace for Unity's pass-ball port (Pota spec 2026-10-11 §1), written by Pota's OWN src/renkAvi.ts (read only).
// Copy it OUTSIDE both repos and run it from the Pota worktree (its tsx), then commit the json into Unity:
//   cp Tools/potalook/pass_fixtures.ts /tmp/pass_fixtures.ts && cd ~/projects/Pota-kombo && \
//   POTA_REV="$(git rev-parse --short HEAD) renkAvi.ts $(shasum -a 256 src/renkAvi.ts | cut -c1-12)" npx tsx /tmp/pass_fixtures.ts \
//     "$HOME/projects/Unity MCP/Assets/_Project/Games/Pota/Tests/EditMode/Fixtures/pass-rules.json"
// Read by PassRulesTests (EditMode). Since Pota's kombo round (2026-10-13) the pass target prefers Chebyshev ≥ 3 (K7); combos are off
// here (Pota renk-avi-test moved its "bomb area fires a pass" board to the diagonal for the same reason) and traced by combo_fixtures.ts,
// except on the Specials scenarios, which mirror Unity's Features.Full (combo on; neither scenario makes one).
// Env POTA_REV (written into the file). Deterministic: no timestamps, every choice
// comes from Pota's own `rastgele`.
import { writeFileSync } from "node:fs";
import * as R from "/Users/tunaaltin17/projects/Pota-kombo/src/renkAvi";
import type { HedefTasarimi, OzelTur, Ozellikler, RAdim, RAlan, RBlok } from "/Users/tunaaltin17/projects/Pota-kombo/src/renkAvi";

const OUT = process.argv[2];
if (!OUT) throw new Error("usage: tsx pass_fixtures.ts <out.json>");

/** Unity SpecialKind (None 0). */
const KIND: Record<OzelTur, number> = { cizgiDikey: 1, cizgiYatay: 2, bomba: 3, altin: 4, pasTopu: 5 };
const LETTER: Record<string, OzelTur> = { C: "cizgiDikey", Y: "cizgiYatay", B: "bomba", A: "altin", P: "pasTopu" };

/** renk-avi-test.ts `tahta`: a digit per colour, letters for specials, hp 1, ammo 100, 5 colours, rng 1, OZELLIK_HEPSI. */
function tahta(rows: string[], ek: Partial<RAlan> = {}): RAlan {
  const n = rows.length;
  const hucreler: RBlok[] = [];
  for (const s of rows) {
    if (s.length !== n) throw new Error(`not square: ${s}`);
    for (const ch of s) hucreler.push(LETTER[ch] ? R.ozelBlok(LETTER[ch]) : { renk: Number(ch), can: 1 });
  }
  return { boyut: n, hucreler, cephane: 100, hedefler: [], renkSayisi: 5, can: { alt: 1, ust: 1 }, rng: 1, ozellikler: R.OZELLIK_HEPSI,
    altin: 0, hakemFile: R.HAKEM_FILE_VARSAYILAN, altinVurus: 3, hakemKuyruk: [], hakemAyniAnda: 1, ...ek };
}

// ---------------------------------------------------------------- JSON (Unity JsonUtility: objects and flat arrays only)

const cellKinds = (l: { hucre: number; tur: OzelTur }[] | undefined) => (l ?? []).map((d) => ({ cell: d.hucre, kind: KIND[d.tur] }));
const targetJson = (h: HedefTasarimi) => ({ kind: h.tur ?? "renk", color: h.renk ?? -1, count: h.adet });

function stepJson(a: RAdim) {
  if (a.altin?.length) throw new Error("golden flight in a traced step: Unity's golden ball is the old gate/basket one");
  if (a.fileHasar?.length || a.engelKalkan?.length || a.hakem?.length || a.hakemGelen?.length) throw new Error("obstacle in a traced step");
  if (a.kombo) throw new Error("combo in a pass trace: combos are combo_fixtures.ts's");
  return {
    exploded: a.patlayan.slice(),
    triggered: cellKinds(a.tetiklenen),
    passes: (a.pasTopu ?? []).map((p) => ({ cell: p.hucre, target: p.hedef })),
    born: cellKinds(a.dogan),
    drops: a.dususler.map((d) => ({ from: d.kaynak, to: d.hedef })),
    spawns: a.yeniler.map((y) => ({ to: y.hedef, color: y.blok.renk, hp: y.blok.can, distance: y.mesafe })),
  };
}

function boardJson(a: RAlan) {
  for (const b of a.hucreler) if (b.engel) throw new Error("obstacle on a traced board");
  return {
    size: a.boyut,
    colors: a.hucreler.map((b) => b.renk),
    hp: a.hucreler.map((b) => b.can),
    specials: a.hucreler.map((b) => (b.ozel ? KIND[b.ozel] : 0)),
    ammo: a.cephane,
    rng: a.rng >>> 0,
    targetsLeft: a.hedefler.map((h) => h.kalan),
  };
}

const features = (o: Ozellikler) => ({ line: o.cizgi, bomb: o.bomba, golden: o.altin, pass: o.pasTopu, combo: !!o.kombo });

// ---------------------------------------------------------------- 1) matches: eslesmeleriCoz (Task 1's boards, renk-avi-test.ts §"2×2 kare")

/** OZELLIK_TAM without combos: the pass ball alone (a pass next to a bomb would merge with it). */
const PAS: Ozellikler = { ...R.OZELLIK_TAM, kombo: false };
const TAM = { ozellikler: PAS };
const MATCHES: { name: string; rows: string[]; ek: Partial<RAlan> }[] = [
  { name: "k1", rows: ["00123", "00456", "78912", "45678", "12345"], ek: TAM },
  { name: "k1-off", rows: ["00123", "00456", "78912", "45678", "12345"], ek: {} },
  { name: "p", rows: ["00012", "00345", "67812", "34567", "81234"], ek: TAM },
  { name: "c4", rows: ["00001", "00234", "56781", "23456", "78123"], ek: TAM },
  { name: "a5", rows: ["00000", "00123", "45678", "12345", "67812"], ek: TAM },
  { name: "l", rows: ["00012", "00345", "06781", "23456", "78123"], ek: TAM },
  { name: "b23", rows: ["00012", "00034", "56781", "23456", "78123"], ek: TAM },
  { name: "b23-off", rows: ["00012", "00034", "56781", "23456", "78123"], ek: {} },
  { name: "b33", rows: ["00012", "00034", "00056", "12345", "67812"], ek: TAM },
  { name: "iki", rows: ["00112", "00113", "45678", "23456", "78234"], ek: TAM },
  { name: "special-in-square", rows: ["00123", "0P456", "78912", "45678", "12345"], ek: TAM },
];
const matches = MATCHES.map(({ name, rows, ek }) => {
  const a = tahta(rows, ek);
  const c = R.eslesmeleriCoz(a);
  return {
    name, rows, features: features(a.ozellikler),
    squares: R.kareleriBul(a).map((q: { renk: number; hucreler: number[] }) => ({ color: q.renk, cells: q.hucreler.slice() })),
    hasMatch: R.eslesmeVarMi(a),
    exploded: c.patlayan, born: cellKinds(c.dogan),
    groups: c.gruplar.map((g: { hucre: number; hucreler: number[] }) => ({ cell: g.hucre, cells: g.hucreler.slice() })),
  };
});

// ---------------------------------------------------------------- 2) taps: kir on a pass (Task 3's boards, §"pas topu: dokununca")

const UCUS = { ozellikler: PAS, renkSayisi: 1000, rng: 3 };
const TAPS: { name: string; rows: string[]; targets: HedefTasarimi[]; tap: number; rotate?: "saat" | "ters"; probes?: { exclude: number[]; noTargets?: boolean }[] }[] = [
  { name: "t", rows: ["31245", "12451", "24P12", "45124", "51243"], targets: [{ renk: 3, adet: 5 }], tap: 12,
    probes: [{ exclude: [12] }, { exclude: [12], noTargets: true }] },
  { name: "pb", rows: ["B1245", "12451", "24P12", "45124", "51243"], targets: [{ tur: "bomba", adet: 2 }, { tur: "cizgi", adet: 2 }], tap: 12 },
  { name: "ppp", rows: ["B1245", "1P451", "24P12", "45124", "51243"], targets: [{ tur: "bomba", adet: 1 }], tap: 12 },
  { name: "bp", rows: ["01245", "1B451", "2P412", "45124", "51243"], targets: [{ renk: 3, adet: 3 }], tap: 6 },
  { name: "bos", rows: ["PBB", "BBB", "BBB"], targets: [], tap: 0 },
  { name: "t-rotate", rows: ["31245", "12451", "24P12", "45124", "51243"], targets: [{ renk: 3, adet: 5 }], tap: -1, rotate: "saat" },
  { name: "pc", rows: ["31245", "12451", "24P12", "4C124", "51243"], targets: [{ tur: "cizgi", adet: 1 }], tap: 12 },
];
const taps = TAPS.map((c) => {
  let a = tahta(c.rows, { ...UCUS, hedefler: c.targets.map(R.hedefKur) });
  const start = boardJson(a);
  let rotatedSteps = -1, tap = c.tap;
  if (c.rotate) {
    const d = R.dondurVeOtur(a, c.rotate);
    rotatedSteps = d.adimlar.length;
    a = d.alan;
    tap = a.hucreler.findIndex((b: RBlok) => b.ozel === "pasTopu");
  }
  const probes = (c.probes ?? []).map((p) => {
    const b = p.noTargets ? { ...a, hedefler: [] } : a;
    return { exclude: p.exclude, noTargets: !!p.noTargets, expect: R.pasHedefi(b, tap, new Set(p.exclude)) ?? -1 };
  });
  const k = R.kir(a, tap);
  return {
    name: c.name, rows: c.rows, colorCount: 1000, rng: 3, ammo: 100, features: features(a.ozellikler), targets: c.targets.map(targetJson),
    start, rotate: c.rotate ?? "", rotatedSteps, rotated: c.rotate ? boardJson(a) : null, tap, probes,
    result: k.sonuc, cost: k.maliyet, steps: k.adimlar.map(stepJson), after: boardJson(k.alan),
  };
});

// ---------------------------------------------------------------- 3) deals: tahtaKur with pass on (no square, no triple)

const DESIGN = { boyut: 7, renkSayisi: 5, can: { alt: 3, ust: 14 }, hedefler: [{ renk: 0, adet: 22 }, { renk: 1, adet: 16 }] };
const NO_GOLD: Ozellikler = { cizgi: true, bomba: true, altin: false, pasTopu: true, kombo: false };
const deals = [1, 2, 3, 42, 99].map((seed) => {
  const a = R.tahtaKur({ ...DESIGN, ozellikler: R.OZELLIK_TAM }, 158, seed);
  if (R.eslesmeVarMi(a)) throw new Error(`seed ${seed}: deal has a match`);
  return { seed, features: features(a.ozellikler), board: boardJson(a) };
});

// ---------------------------------------------------------------- 4) replays: random games with pass on, golden off (Unity's golden is the old one)

const REPLAY_AMMO = 600;
const replays: unknown[] = [];
let passFlights = 0;
for (const seed of [11, 12, 13, 14, 15, 16, 17, 18]) {
  let a: RAlan = R.tahtaKur({ ...DESIGN, ozellikler: NO_GOLD }, REPLAY_AMMO, seed);
  const start = boardJson(a);
  const moves: unknown[] = [];
  let rd = seed * 7919;
  for (let m = 0; m < 60; m++) {
    const s = R.rastgele(rd); rd = s.durum;
    if (s.deger < 0.12) {
      const dir = s.deger < 0.06 ? "saat" : "ters";
      const d = R.dondurVeOtur(a, dir);
      moves.push({ kind: "rotate", cell: -1, dir, result: "", cost: 0, steps: d.adimlar.map(stepJson), after: boardJson(d.alan) });
      a = d.alan;
      continue;
    }
    // Specials first when there are any (the pass is what is traced), else a block the ammo covers.
    const specials = a.hucreler.map((b, i) => (b.ozel ? i : -1)).filter((i) => i >= 0);
    const cheap = a.hucreler.map((b, i) => (!b.ozel && b.can <= a.cephane ? i : -1)).filter((i) => i >= 0);
    const pool = specials.length && s.deger < 0.7 ? specials : cheap;
    if (!pool.length) break;
    const s2 = R.rastgele(rd); rd = s2.durum;
    const cell = pool[Math.floor(s2.deger * pool.length)]!;
    const k = R.kir(a, cell);
    for (const st of k.adimlar) passFlights += st.pasTopu?.length ?? 0;
    moves.push({ kind: "tap", cell, dir: "", result: k.sonuc, cost: k.maliyet, steps: k.adimlar.map(stepJson), after: boardJson(k.alan) });
    a = k.alan;
  }
  replays.push({ name: `seed${seed}`, seed, ammo: REPLAY_AMMO, features: features(NO_GOLD), start, moves });
}
if (passFlights === 0) throw new Error("no pass flew in the replays");

// ---------------------------------------------------------------- 5) Unity's Specials scenarios Pass / PassBomb (SpecialScenarios.cs grids)

/** Unity BoardPreset → board: base (2r + c) mod 5 with overrides, hp HpMin + (7i mod 12), 158 balls, rng 1, OZELLIK_TAM. */
function presetBoard(over: Record<number, number | OzelTur>, targets: HedefTasarimi[]): RAlan {
  const h: RBlok[] = [];
  for (let i = 0; i < 49; i++) {
    const r = Math.floor(i / 7), c = i % 7, o = over[i];
    h.push(typeof o === "string" ? R.ozelBlok(o) : { renk: o ?? (2 * r + c) % 5, can: 3 + (i * 7) % 12 });
  }
  return { boyut: 7, hucreler: h, cephane: 158, hedefler: targets.map(R.hedefKur), renkSayisi: 5, can: { alt: 3, ust: 14 }, rng: 1,
    ozellikler: R.OZELLIK_TAM, altin: 0, hakemFile: R.HAKEM_FILE_VARSAYILAN, altinVurus: 3, hakemKuyruk: [], hakemAyniAnda: 1 };
}
const SCENARIOS: { name: string; over: Record<number, number | OzelTur>; targets: HedefTasarimi[]; taps: number[] }[] = [
  // Pass: tap 31 (4,3) — the 2 above drops in → a 2×2 of 2s at rows 4–5, cols 2–3 → a pass ball at its bottom-left (5,2) = 37; tap it.
  { name: "pass", over: { 30: 2, 38: 2, 24: 2, 17: 3, 32: 0 }, targets: [{ renk: 2, adet: 12 }], taps: [31, 37] },
  // PassBomb: a pass at (4,1) = 29, a bomb at (2,5) = 19, only a bomb target: the pass flies to the bomb, which bursts on arrival.
  { name: "passBomb", over: { 29: "pasTopu", 19: "bomba" }, targets: [{ tur: "bomba", adet: 2 }], taps: [29] },
];
const scenarios = SCENARIOS.map((sc) => {
  let a = presetBoard(sc.over, sc.targets);
  if (R.eslesmeVarMi(a)) throw new Error(`${sc.name}: the preset starts with a match`);
  const start = boardJson(a);
  const moves = sc.taps.map((cell) => {
    const k = R.kir(a, cell);
    a = k.alan;
    return { kind: "tap", cell, dir: "", result: k.sonuc, cost: k.maliyet, steps: k.adimlar.map(stepJson), after: boardJson(k.alan) };
  });
  return { name: sc.name, targets: sc.targets.map(targetJson), start, moves };
});

const out = {
  version: 1,
  pota: process.env.POTA_REV ?? "",
  design: { size: 7, colorCount: 5, hpMin: 3, hpMax: 14, ammo: 158, targets: [{ color: 0, count: 22 }, { color: 1, count: 16 }] },
  matches, taps, deals, replays, scenarios, passFlights,
};
writeFileSync(OUT, JSON.stringify(out) + "\n");
console.log(`${OUT}: ${matches.length} matches, ${taps.length} taps, ${deals.length} deals, ${replays.length} replays (${passFlights} pass flights), ${scenarios.length} scenarios`);
