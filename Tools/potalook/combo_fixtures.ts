// Golden trace for Unity's combo port (Pota spec 2026-10-13 §1, §3), written by Pota's OWN src/renkAvi.ts + src/ozelPlan.ts (read only).
// Copy it OUTSIDE both repos and run it from the Pota worktree (its tsx), then commit the json into Unity:
//   cp Tools/potalook/combo_fixtures.ts /tmp/combo_fixtures.ts && cd ~/projects/Pota-kombo && \
//   POTA_REV="$(git rev-parse --short HEAD) renkAvi.ts $(shasum -a 256 src/renkAvi.ts | cut -c1-12) ozelPlan.ts $(shasum -a 256 src/ozelPlan.ts | cut -c1-12)" \
//     npx tsx /tmp/combo_fixtures.ts "$HOME/projects/Unity MCP/Assets/_Project/Games/Pota/Tests/EditMode/Fixtures/combo-rules.json"
// Read by ComboRulesTests (EditMode). Env POTA_REV (written into the file). Deterministic: no timestamps, every choice comes from Pota's own
// `rastgele`. Unity has no obstacles (file/ice/referee) and its golden ball is the old gate/basket one (U25): traced boards carry neither
// (the generator throws if one shows up); Pota's obstacle and golden combo tests are probes only where Unity can say the same thing.
import { writeFileSync } from "node:fs";
import * as R from "/Users/tunaaltin17/projects/Pota-kombo/src/renkAvi";
import * as P from "/Users/tunaaltin17/projects/Pota-kombo/src/ozelPlan";
import type { HedefTasarimi, KomboTur, OzelTur, Ozellikler, RAdim, RAlan, RBlok } from "/Users/tunaaltin17/projects/Pota-kombo/src/renkAvi";

const OUT = process.argv[2];
if (!OUT) throw new Error("usage: tsx combo_fixtures.ts <out.json>");

/** Unity SpecialKind (None 0) and ComboKind names. */
const KIND: Record<OzelTur, number> = { cizgiDikey: 1, cizgiYatay: 2, bomba: 3, altin: 4, pasTopu: 5 };
const COMBO: Record<KomboTur, string> = { arti: "Cross", kalinArti: "ThickCross", devBomba: "BigBomb", pasCizgi: "PassLine", pasBomba: "PassBomb", pasPas: "PassPass" };
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

// renk-avi-test.ts "kombo I": 7×7 base, each row the previous one shifted by 2 (no two neighbours alike); 1000 colours → refills make no match.
const KOMBO_TAB = ["0123401", "2340123", "4012340", "1234012", "3401234", "0123401", "2340123"];
const kyaz = (degis: Record<number, string>): string[] => KOMBO_TAB.map((s, r) => [...s].map((ch, c) => degis[r * 7 + c] ?? ch).join(""));

// ---------------------------------------------------------------- JSON (Unity JsonUtility: objects and flat arrays only)

const cellKinds = (l: { hucre: number; tur: OzelTur }[] | undefined) => (l ?? []).map((d) => ({ cell: d.hucre, kind: KIND[d.tur] }));
const targetJson = (h: HedefTasarimi) => ({ kind: h.tur ?? "renk", color: h.renk ?? -1, count: h.adet });
const NO_COMBO = { kind: "", center: -1, partner: -1, carried: 0, passTargets: [] as number[] };

function comboJson(a: RAdim) {
  const k = a.kombo;
  if (!k) return NO_COMBO;
  return { kind: COMBO[k.tur], center: k.merkez, partner: k.es, carried: k.tasinan ? KIND[k.tasinan] : 0, passTargets: (k.pasHedefleri ?? []).slice() };
}

function stepJson(a: RAdim) {
  if (a.altin?.length) throw new Error("golden flight in a traced step: Unity's golden ball is the old gate/basket one");
  if (a.fileHasar?.length || a.engelKalkan?.length || a.hakem?.length || a.hakemGelen?.length) throw new Error("obstacle in a traced step");
  return {
    exploded: a.patlayan.slice(),
    triggered: cellKinds(a.tetiklenen),
    passes: (a.pasTopu ?? []).map((p) => ({ cell: p.hucre, target: p.hedef })),
    combo: comboJson(a),
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

// ---------------------------------------------------------------- plans: ozelPlani on every special tap's first step

const Z = P.OZEL_ZAMANLAMA;
/** The fixture's swell (renkAviFaz sisme for combos, ozel-plan-test kombo block) and Pota's swell peak (balon.tepe). */
const SISME = 0.3, TEPE = 0.55;
const timing = { lineSpeed: Z.cizgiHiz, lineLead: Z.cizgiOncu, bombLead: Z.bombaOncu, bombRing: Z.bombaHalka, cap: Z.tavan,
  passTakeoff: Z.pasKalkis, passSpeed: Z.pasHiz, passMin: Z.pasEnAz, passMax: Z.pasEnCok, passHang: Z.pasAsili };
const planJson = (p: P.OzelPlani) => ({
  fires: p.atesler.map((a) => ({ cell: a.hucre, kind: KIND[a.tur], order: a.sira, t: a.t, size: a.boyut })),
  starts: [...p.baslangic].map(([cell, t]) => ({ cell, t })),
  pace: p.hiz,
  end: p.sure,
});
const plans: unknown[] = [];
/**
 * Two Pota plans per step: `pota` with the game's swell peak (tepe 0.55) and `flat` with the peak at the swell's start (tepe 0). Unity's
 * SpecialSchedule fires the tapped special at the step's first PEAK and starts a cell's swell at its arrival, so its plan is `flat` with
 * every fire (and every pass landing) moved by sisme × 0.55 — see ComboRulesTests.Plan for the whole mapping.
 */
function addPlan(name: string, n: number, a: RAdim, origin: number) {
  if (!a.tetiklenen?.length) return;
  plans.push({
    name, n, origin, swell: SISME, peakAt: TEPE, timing, step: stepJson(a),
    pota: planJson(P.ozelPlani(n, a, origin, Z, SISME, TEPE)),
    flat: planJson(P.ozelPlani(n, a, origin, Z, SISME, 0)),
  });
}

// ---------------------------------------------------------------- 1) taps: kir on Pota's hand-traced boards (renk-avi-test.ts kombo I, II, K7)

const KK = { ozellikler: R.OZELLIK_TAM, renkSayisi: 1000, rng: 3 };
type Tap = { name: string; rows: string[]; targets: HedefTasarimi[]; tap: number; ek?: Partial<RAlan>; probes?: { exclude: number[]; noTargets?: boolean }[] };
const r3 = (adet: number): HedefTasarimi => ({ renk: 3, adet });
const r7 = (adet: number): HedefTasarimi => ({ renk: 7, adet });
const TAPS: Tap[] = [
  // kombo I (a) artı: C 24 + Y 25, tap 24 → row 3 + column 3; tap 25 → centre 25. Combo off (OZELLIK_HEPSI): the line alone.
  { name: "cross", rows: kyaz({ 24: "C", 25: "Y" }), targets: [], tap: 24 },
  { name: "cross-25", rows: kyaz({ 24: "C", 25: "Y" }), targets: [], tap: 25 },
  { name: "cross-off", rows: kyaz({ 24: "C", 25: "Y" }), targets: [], tap: 24, ek: { ozellikler: R.OZELLIK_HEPSI } },
  // (b) kalın artı: C 24 + B 31 (both count: line 2 → 1, bomb 2 → 1); corner B 0 + C 1.
  { name: "thick", rows: kyaz({ 24: "C", 31: "B" }), targets: [{ tur: "cizgi", adet: 2 }, { tur: "bomba", adet: 2 }], tap: 24 },
  { name: "thick-corner", rows: kyaz({ 0: "B", 1: "C" }), targets: [], tap: 0 },
  // (c) dev bomba: B 23 + B 24 → 5×5; corner B 0 + B 7 → 3×3.
  { name: "bigbomb", rows: kyaz({ 23: "B", 24: "B" }), targets: [], tap: 24 },
  { name: "bigbomb-corner", rows: kyaz({ 0: "B", 7: "B" }), targets: [], tap: 0 },
  // (d) priority and ties: thick > cross, thick > big bomb, the smaller index on a tie.
  { name: "prio-thick-cross", rows: kyaz({ 17: "C", 23: "B", 24: "C" }), targets: [], tap: 24 },
  { name: "prio-thick-bigbomb", rows: kyaz({ 17: "B", 24: "B", 25: "C" }), targets: [], tap: 24 },
  { name: "prio-tie", rows: kyaz({ 23: "Y", 24: "C", 25: "C" }), targets: [], tap: 24 },
  // (e) diagonal is no partner; tapping the bomb finds the line too.
  { name: "diagonal", rows: kyaz({ 24: "C", 32: "B" }), targets: [], tap: 24 },
  { name: "thick-on-bomb", rows: kyaz({ 24: "C", 25: "B" }), targets: [], tap: 25 },
  // (f) the area's other specials chain with their own area; no combo in the chain (K4).
  { name: "chain", rows: kyaz({ 2: "C", 3: "B", 24: "C", 25: "Y" }), targets: [], tap: 24 },
  // kombo II (a) pas + bomba: the carried bomb bursts at 3 (≥ 3, nearest); (b) pas + çizgi; (c) üç pas 3, 13, 28; (d) nothing to carry to.
  { name: "pass-bomb", rows: kyaz({ 24: "P", 25: "B" }), targets: [r3(9), { tur: "bomba", adet: 2 }], tap: 24 },
  { name: "pass-line", rows: kyaz({ 23: "C", 24: "P" }), targets: [r3(9)], tap: 24 },
  { name: "pass-pass", rows: kyaz({ 24: "P", 25: "P" }), targets: [r3(9)], tap: 24 },
  { name: "carry-nowhere", rows: ["PBC", "CCC", "CCC"], targets: [], tap: 0 },
  // P1: carrying, the target is never a special — not even one in a target (the bomb at 3, the lines); three passes may hit one.
  { name: "carry-skips-target-bomb", rows: kyaz({ 3: "B", 24: "P", 25: "B" }), targets: [{ tur: "bomba", adet: 2 }], tap: 24 },
  { name: "carry-nowhere-line-target", rows: ["PBC", "CCC", "CCC"], targets: [{ tur: "cizgi", adet: 3 }], tap: 0 },
  { name: "pass-pass-hits-target-bomb", rows: kyaz({ 3: "B", 24: "P", 25: "P" }), targets: [{ tur: "bomba", adet: 1 }], tap: 24 },
  // (e) priority over pass combos; (f) a single pass hitting a bomb (next to a line) stays single (K4).
  { name: "prio-cross-passline", rows: kyaz({ 23: "P", 24: "C", 25: "C" }), targets: [], tap: 24 },
  { name: "prio-passbomb-passline", rows: kyaz({ 24: "P", 25: "C", 31: "B" }), targets: [], tap: 24 },
  { name: "pass-hits-bomb", rows: kyaz({ 3: "B", 4: "C", 24: "P" }), targets: [{ tur: "bomba", adet: 1 }], tap: 24 },
  // K7: Chebyshev ≥ 3's nearest (21 over 7 and 42); none ≥ 3 → the farthest (16); no target, 7×7 centre → distance 3, smallest index; in a target first.
  { name: "k7-nearest-far", rows: kyaz({ 0: "P", 7: "7", 21: "7", 42: "7" }), targets: [r7(3)], tap: 0, probes: [{ exclude: [0] }] },
  { name: "k7-farthest", rows: kyaz({ 0: "P", 7: "7", 16: "7" }), targets: [r7(2)], tap: 0, probes: [{ exclude: [0] }] },
  { name: "k7-no-target", rows: kyaz({ 24: "P" }), targets: [], tap: 24, probes: [{ exclude: [24] }] },
  { name: "k7-target-first", rows: kyaz({ 24: "P", 17: "7" }), targets: [r7(1)], tap: 24, probes: [{ exclude: [24] }, { exclude: [24], noTargets: true }] },
];
const comboCount: Record<string, number> = { Cross: 0, ThickCross: 0, BigBomb: 0, PassLine: 0, PassBomb: 0, PassPass: 0 };
const taps = TAPS.map((c) => {
  const a = tahta(c.rows, { ...KK, hedefler: c.targets.map(R.hedefKur), ...c.ek });
  const probes = (c.probes ?? []).map((p) => {
    const b = p.noTargets ? { ...a, hedefler: [] } : a;
    return { exclude: p.exclude, noTargets: !!p.noTargets, expect: R.pasHedefi(b, c.tap, new Set(p.exclude)) ?? -1 };
  });
  const partner = R.komboOrtagi(a, c.tap) ?? -1;
  const k = R.kir(a, c.tap);
  const first = k.adimlar[0];
  if (first) addPlan(`tap ${c.name}`, a.boyut, first, c.tap);
  return {
    name: c.name, rows: c.rows, colorCount: a.renkSayisi, rng: a.rng, ammo: a.cephane, features: features(a.ozellikler), targets: c.targets.map(targetJson),
    start: boardJson(a), tap: c.tap, partner, probes, result: k.sonuc, cost: k.maliyet, steps: k.adimlar.map(stepJson), after: boardJson(k.alan),
  };
});

// ---------------------------------------------------------------- 2) partner and pair probes (komboOrtagi, komboCiftleri), closure (alanKapanisi)

const PARTNERS: { name: string; rows: string[]; cell: number; ek?: Partial<RAlan> }[] = [
  { name: "golden-is-no-partner", rows: kyaz({ 24: "C", 25: "A" }), cell: 24 },
  { name: "bomb-finds-line", rows: kyaz({ 24: "C", 25: "B" }), cell: 25 },
  { name: "normal-block-tap", rows: kyaz({ 25: "B" }), cell: 24 },
  { name: "golden-tapped", rows: kyaz({ 24: "A", 25: "B" }), cell: 24 },
  { name: "combo-off", rows: kyaz({ 24: "C", 25: "Y" }), cell: 24, ek: { ozellikler: R.OZELLIK_HEPSI } },
  { name: "edge-left", rows: kyaz({ 21: "B", 14: "P" }), cell: 21 },
  { name: "edge-wrap", rows: kyaz({ 20: "B", 21: "C" }), cell: 21 },
];
const partners = PARTNERS.map((p) => {
  const a = tahta(p.rows, { ...KK, ...p.ek });
  return { name: p.name, rows: p.rows, features: features(a.ozellikler), cell: p.cell, expect: R.komboOrtagi(a, p.cell) ?? -1 };
});
const PAIRS: { name: string; rows: string[] }[] = [
  { name: "neighbours-only", rows: kyaz({ 24: "C", 25: "Y", 40: "B" }) },
  { name: "row-of-three", rows: kyaz({ 22: "P", 23: "C", 24: "B", 30: "A" }) },
];
const pairs = PAIRS.map((p) => {
  const a = tahta(p.rows, KK);
  return { name: p.name, rows: p.rows, features: features(a.ozellikler), pairs: [...R.komboCiftleri(a)].map(([cell, partner]) => ({ cell, partner })) };
});
const CLOSURES: { name: string; rows: string[]; start: number[]; consumed: [number, OzelTur][] }[] = [
  { name: "consumed-open-nothing", rows: kyaz({ 24: "C", 25: "B" }), start: [24, 25], consumed: [[24, "cizgiDikey"], [25, "bomba"]] },
  { name: "cross-chains-bomb", rows: kyaz({ 2: "C", 3: "B", 24: "C", 25: "Y" }), start: R.komboAlanHucreleri(7, 24, "arti"), consumed: [[24, "cizgiDikey"], [25, "cizgiYatay"]] },
  { name: "golden-left-out", rows: kyaz({ 10: "A", 24: "B", 23: "B" }), start: R.komboAlanHucreleri(7, 24, "devBomba"), consumed: [[24, "bomba"], [23, "bomba"]] },
];
const closures = CLOSURES.map((c) => {
  const a = tahta(c.rows, KK);
  const p = R.alanKapanisi(a, c.start, new Map(c.consumed));
  return { name: c.name, rows: c.rows, start: c.start, consumed: c.consumed.map(([cell, tur]) => ({ cell, kind: KIND[tur] })), cells: p.hucreler, triggered: cellKinds(p.tetiklenen) };
});
const TURLER: OzelTur[] = ["cizgiDikey", "cizgiYatay", "bomba", "altin", "pasTopu"];
const kindsOf = TURLER.flatMap((a) => TURLER.map((b) => { const k = R.komboTuru(a, b); return { a: KIND[a], b: KIND[b], kind: k ? COMBO[k] : "" }; }));
const comboCells = (["arti", "kalinArti", "devBomba"] as const).flatMap((tur) => [0, 3, 24, 48].map((center) => ({ kind: COMBO[tur], n: 7, center, cells: R.komboAlanHucreleri(7, center, tur) })));
const delays = (["arti", "kalinArti", "devBomba"] as const).flatMap((tur) => [24, 0].flatMap((center) =>
  R.komboAlanHucreleri(7, center, tur).map((cell) => ({ kind: COMBO[tur], n: 7, center, cell, t: P.komboGecikme(7, center, tur, cell, Z) }))));

// ---------------------------------------------------------------- 3) replays: random games, combo on, golden off (Unity's golden is the old one)

const DESIGN = { boyut: 7, renkSayisi: 5, can: { alt: 3, ust: 14 }, hedefler: [{ renk: 0, adet: 22 }, { renk: 1, adet: 16 }] };
/** OZELLIK_TAM (levels 9–50) without the golden ball: the pass ball, its K7 targets and every combo are traced together. */
const NO_GOLD: Ozellikler = { ...R.OZELLIK_TAM, altin: false };
const REPLAY_AMMO = 600;
const replays: unknown[] = [];
let passFlights = 0;
// Combos are rare in random play (≤ 2 per 60-move game over seeds 1–300 with this policy): these eight seeds carry two each and
// every kind (Cross 24·187, ThickCross 49·202·264×2, BigBomb 24·148, PassLine 49·202·223, PassBomb 3·148·187, PassPass 3·223).
for (const seed of [3, 24, 49, 148, 187, 202, 223, 264]) {
  let a: RAlan = R.tahtaKur({ ...DESIGN, ozellikler: NO_GOLD }, REPLAY_AMMO, seed);
  const start = boardJson(a);
  const moves: unknown[] = [];
  let rd = seed * 7919;
  for (let m = 0; m < 60; m++) {
    const s = R.rastgele(rd); rd = s.durum;
    if (s.deger < 0.12) {
      const dir = s.deger < 0.06 ? "saat" : "ters";
      const d = R.dondurVeOtur(a, dir);
      moves.push({ kind: "rotate", cell: -1, dir, result: "", cost: 0, partner: -1, steps: d.adimlar.map(stepJson), after: boardJson(d.alan) });
      a = d.alan;
      continue;
    }
    // A special with a partner first (the combo is what is traced), then any special, else a block the ammo covers.
    const pairsNow = [...R.komboCiftleri(a).keys()];
    const specials = a.hucreler.map((b, i) => (b.ozel ? i : -1)).filter((i) => i >= 0);
    const cheap = a.hucreler.map((b, i) => (!b.ozel && b.can <= a.cephane ? i : -1)).filter((i) => i >= 0);
    const pool = pairsNow.length && s.deger < 0.6 ? pairsNow : specials.length && s.deger < 0.75 ? specials : cheap;
    if (!pool.length) break;
    const s2 = R.rastgele(rd); rd = s2.durum;
    const cell = pool[Math.floor(s2.deger * pool.length)]!;
    const partner = R.komboOrtagi(a, cell) ?? -1;
    const k = R.kir(a, cell);
    for (const st of k.adimlar) passFlights += st.pasTopu?.length ?? 0;
    const first = k.adimlar[0];
    if (first?.kombo) comboCount[COMBO[first.kombo.tur]]!++;
    if (first) addPlan(`seed${seed} move ${m}`, a.boyut, first, cell);
    moves.push({ kind: "tap", cell, dir: "", result: k.sonuc, cost: k.maliyet, partner, steps: k.adimlar.map(stepJson), after: boardJson(k.alan) });
    a = k.alan;
  }
  replays.push({ name: `seed${seed}`, seed, ammo: REPLAY_AMMO, features: features(NO_GOLD), start, moves });
}
for (const [kind, count] of Object.entries(comboCount)) if (count === 0) throw new Error(`no ${kind} combo in the replays`);
if (passFlights === 0) throw new Error("no pass flew in the replays");
for (const t of taps) for (const st of t.steps) if (st.combo.kind) comboCount[st.combo.kind]!++;

const out = {
  version: 1,
  pota: process.env.POTA_REV ?? "",
  design: { size: 7, colorCount: 5, hpMin: 3, hpMax: 14, ammo: REPLAY_AMMO, targets: [{ color: 0, count: 22 }, { color: 1, count: 16 }] },
  minPassDistance: R.PAS_EN_AZ_UZAKLIK, comboPassCount: R.KOMBO_PAS_SAYISI,
  priority: Object.entries(R.KOMBO_ONCELIK).map(([tur, p]) => ({ kind: COMBO[tur as KomboTur], priority: p })),
  kindsOf, taps, partners, pairs, closures, comboCells, delays, plans, replays, passFlights,
  comboCounts: Object.entries(comboCount).map(([kind, count]) => ({ kind, count })),
};
writeFileSync(OUT, JSON.stringify(out) + "\n");
console.log(`${OUT}: ${taps.length} taps, ${partners.length} partner probes, ${pairs.length} pair probes, ${closures.length} closures, ${plans.length} plans, ` +
  `${replays.length} replays (${passFlights} pass flights); combos ${Object.entries(comboCount).map(([k, v]) => `${k} ${v}`).join(", ")}`);
