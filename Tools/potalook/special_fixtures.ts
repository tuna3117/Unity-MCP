// Fixtures for Unity's Color Hunt rules, produced by Pota's OWN rules (src/renkAvi.ts). Spec 2026-10-01 special balls §3.
// Runs only inside a SCRATCH COPY of Pota: Tools/potalook/run_fixtures.sh copies this file to <copy>/arac/ and runs it with
// the copy's tsx. Never run it inside ~/projects/Pota.
//   tsx arac/unity-special-fixtures.ts <out.json>        (env POTA_REV = Pota's commit + renkAvi.ts hash, written into the file)
// Deterministic: the same Pota code writes the same bytes (no timestamps; every choice comes from Pota's own `rastgele`).
import { writeFileSync } from "node:fs";
import {
  dondurVeOtur, hazirAltin, kir, kopyala, ozelBlok, rastgele, tahtaKur,
  OZELLIK_HEPSI, OZELLIK_YOK, type OzelTur, type Ozellikler, type RAdim, type RAlan, type RTasarim, type Yon,
} from "../src/renkAvi";

const OUT = process.argv[2];
if (!OUT) throw new Error("usage: tsx arac/unity-special-fixtures.ts <out.json>");

// Unity's level (ColorHuntSettings defaults, "bölüm 20" in the spec): 7×7, 5 colours, hp 3–14, targets (0,22) (1,16), 158 balls.
const AMMO = 158;
const TASARIM: RTasarim = { boyut: 7, renkSayisi: 5, can: { alt: 3, ust: 14 }, hedefler: [{ renk: 0, adet: 22 }, { renk: 1, adet: 16 }] };
const MOVES = 25;
const NEED = 5;           // every counted event at least this often
const FIRST_SEED = 100;   // all-features games: seeds 100, 101, … until the coverage is reached
const MAX_GAMES = 200;    // … or this many games: then the generator fails

/** Unity SpecialKind values (None 0). Results ("kirildi" | "yetmez" | "altin") and turns ("saat" | "ters") stay Pota's words. */
const KIND: Record<OzelTur, number> = { cizgiDikey: 1, cizgiYatay: 2, bomba: 3, altin: 4 };

// ---------------------------------------------------------------- JSON shapes (Unity JsonUtility: objects and flat arrays only)

interface BoardJson {
  size: number; colors: number[]; hp: number[]; specials: number[]; ammo: number; rng: number;
  gates: number[]; golden: number; targets: { color: number; remaining: number }[];
}
interface StepJson {
  exploded: number[]; drops: { from: number; to: number }[]; spawns: { to: number; color: number; hp: number; distance: number }[];
  born: { cell: number; kind: number }[]; triggered: { cell: number; kind: number }[]; toBasket: { cell: number; col: number }[];
  opened: number[];
}
interface MoveJson {
  kind: "tap" | "rotate"; cell: number; dir: Yon | ""; result: string; cost: number;
  rotated?: BoardJson; after: BoardJson; steps: StepJson[];
}
interface GameJson {
  name: string; seed: number; features: { line: boolean; bomb: boolean; golden: boolean }; dealt: boolean;
  start: BoardJson; moves: MoveJson[];
}

function boardJson(a: RAlan): BoardJson {
  for (const b of a.hucreler) if (b.engel) throw new Error("obstacle on a fixture board");
  for (const h of a.hedefler) if (h.tur !== "renk") throw new Error(`non-colour target ${h.tur}`);
  return {
    size: a.boyut,
    colors: a.hucreler.map((b) => b.renk),
    hp: a.hucreler.map((b) => b.can),
    specials: a.hucreler.map((b) => (b.ozel ? KIND[b.ozel] : 0)),
    ammo: a.cephane,
    rng: a.rng >>> 0,
    gates: a.gecisler.slice(),
    golden: a.altin,
    targets: a.hedefler.map((h) => ({ color: h.renk, remaining: h.kalan })),
  };
}

function stepJson(s: RAdim): StepJson {
  if (s.fileHasar || s.engelKalkan || s.hakem) throw new Error("obstacle step in a fixture");
  return {
    exploded: s.patlayan.slice(),
    drops: s.dususler.map((d) => ({ from: d.kaynak, to: d.hedef })),
    spawns: s.yeniler.map((y) => {
      if (y.blok.ozel || y.blok.engel) throw new Error("a spawned block is special");
      return { to: y.hedef, color: y.blok.renk, hp: y.blok.can, distance: y.mesafe };
    }),
    born: (s.dogan ?? []).map((d) => ({ cell: d.hucre, kind: KIND[d.tur] })),
    triggered: (s.tetiklenen ?? []).map((d) => ({ cell: d.hucre, kind: KIND[d.tur] })),
    toBasket: (s.sepete ?? []).map((x) => ({ cell: x.hucre, col: x.sutun })),
    opened: (s.acilan ?? []).slice(),
  };
}

// ---------------------------------------------------------------- choices (Pota's rastgele, a state of its own)

class Zar {
  private s: number;
  constructor(tohum: number) { this.s = tohum >>> 0; }
  sayi(): number { const r = rastgele(this.s); this.s = r.durum; return r.deger; }
  tam(n: number): number { return Math.floor(this.sayi() * n); }
  sec<T>(l: T[]): T { return l[this.tam(l.length)]!; }
}

type Hamle = { tur: "tap"; hucre: number } | { tur: "rotate"; yon: Yon };
const YONLER: Yon[] = ["saat", "ters"];
const hucreler = (a: RAlan, f: (b: RAlan["hucreler"][number], i: number) => boolean): number[] =>
  a.hucreler.map((b, i) => (f(b, i) ? i : -1)).filter((i) => i >= 0);
const sepeteIner = (adimlar: RAdim[]): boolean => adimlar.some((s) => (s.sepete?.length ?? 0) > 0);

/**
 * One move. Plain play (`akilli` false): 15 % a rotation, else a tap on any cell. Special play looks ahead with Pota's own
 * kir / dondurVeOtur so births, triggers, golden taps, basket drops and rotations into the basket really happen.
 */
function hamleSec(a: RAlan, z: Zar, akilli: boolean): Hamle {
  const r = z.sayi();
  if (akilli) {
    const donus = YONLER.filter((y) => sepeteIner(dondurVeOtur(a, y).adimlar));
    if (donus.length && r < 0.8) return { tur: "rotate", yon: z.sec(donus) };
    const ozel = hucreler(a, (b) => !!b.ozel && b.ozel !== "altin");
    if (ozel.length && r < 0.3) return { tur: "tap", hucre: z.sec(ozel) };
    const altin = hucreler(a, (b) => b.ozel === "altin");
    if (altin.length && r < 0.34) return { tur: "tap", hucre: z.sec(altin) };
    if (r < 0.85) {
      // affordable taps by what they lead to: a golden birth first (the rarest), then a basket drop, then any birth
      const sonuc = hucreler(a, (b) => !b.ozel && b.can <= a.cephane).map((i) => ({ i, adimlar: kir(a, i).adimlar }));
      const olur = (f: (s: RAdim) => boolean) => sonuc.filter((x) => x.adimlar.some(f)).map((x) => x.i);
      for (const iyi of [olur((s) => !!s.dogan?.some((d) => d.tur === "altin")), olur((s) => (s.sepete?.length ?? 0) > 0),
                         olur((s) => (s.dogan?.length ?? 0) > 0)])
        if (iyi.length) return { tur: "tap", hucre: z.sec(iyi) };
    }
  }
  if (r >= 0.85) return { tur: "rotate", yon: z.sec(YONLER) };
  return { tur: "tap", hucre: z.tam(a.boyut * a.boyut) };
}

function oyna(ad: string, tohum: number, o: Ozellikler, baslangic: RAlan, dealt: boolean, akilli: boolean): GameJson {
  const z = new Zar(tohum * 7919 + 17);
  const moves: MoveJson[] = [];
  let a = baslangic;
  for (let m = 0; m < MOVES; m++) {
    const h = hamleSec(a, z, akilli);
    if (h.tur === "rotate") {
      const d = dondurVeOtur(a, h.yon);
      moves.push({ kind: "rotate", cell: -1, dir: h.yon, result: "", cost: 0, rotated: boardJson(d.donmus),
                   after: boardJson(d.alan), steps: d.adimlar.map(stepJson) });
      a = d.alan;
    } else {
      const k = kir(a, h.hucre);
      moves.push({ kind: "tap", cell: h.hucre, dir: "", result: k.sonuc, cost: k.maliyet, after: boardJson(k.alan),
                   steps: k.adimlar.map(stepJson) });
      a = k.alan;
    }
  }
  return { name: ad, seed: tohum, features: { line: o.cizgi, bomb: o.bomba, golden: o.altin }, dealt, start: boardJson(baslangic), moves };
}

/** 3–6 lines and bombs on random cells, then 1–2 golden balls through Pota's own booster (`hazirAltin` opens their gates). */
function yerlestir(a: RAlan, z: Zar): RAlan {
  let b = kopyala(a);
  const adet = 3 + z.tam(4);
  for (let k = 0; k < adet; k++) {
    const tur = z.sec<OzelTur>(["cizgiDikey", "cizgiYatay", "bomba"]);
    b.hucreler[z.sec(hucreler(b, (x) => !x.ozel))] = ozelBlok(tur);
  }
  const altin = 1 + z.tam(2);
  for (let k = 0; k < altin; k++) b = hazirAltin(b);
  return b;
}

// ---------------------------------------------------------------- coverage

const sayac = {
  bornLineV: 0, bornLineH: 0, bornBomb: 0, bornGolden: 0, triggeredLineV: 0, triggeredLineH: 0, triggeredBomb: 0,
  chained: 0, basket: 0, opened: 0, closed: 0, rotateBasket: 0, goldenTaps: 0, insufficient: 0,
};
const BORN: Record<number, keyof typeof sayac> = { 1: "bornLineV", 2: "bornLineH", 3: "bornBomb", 4: "bornGolden" };
const TRIGGERED: Record<number, keyof typeof sayac> = { 1: "triggeredLineV", 2: "triggeredLineH", 3: "triggeredBomb" };

function say(g: GameJson): void {
  let gates = g.start.gates;
  for (const m of g.moves) {
    for (const s of m.steps) {
      for (const b of s.born) sayac[BORN[b.kind]!]++;
      for (const t of s.triggered) sayac[TRIGGERED[t.kind]!]++;
      if (s.triggered.length >= 2) sayac.chained++;
      sayac.basket += s.toBasket.length;
      sayac.opened += s.opened.length;
    }
    sayac.closed += gates.filter((c) => !m.after.gates.includes(c)).length;
    gates = m.after.gates;
    if (m.kind === "rotate" && m.steps.some((s) => s.toBasket.length > 0)) sayac.rotateBasket++;
    if (m.result === "altin") sayac.goldenTaps++;
    if (m.result === "yetmez") sayac.insufficient++;
  }
}
const yeter = (): boolean => Object.values(sayac).every((v) => v >= NEED);

// ---------------------------------------------------------------- games

const CIZGI: Ozellikler = { cizgi: true, bomba: false, altin: false };
const CIZGI_BOMBA: Ozellikler = { cizgi: true, bomba: true, altin: false };
const games: GameJson[] = [];
const ekle = (g: GameJson) => { games.push(g); say(g); };

// (a) features off: the level Unity ships today. Plain random play; Unity's rng use must not move.
for (const tohum of [1, 2, 3, 4]) ekle(oyna(`off-${tohum}`, tohum, OZELLIK_YOK, tahtaKur(TASARIM, AMMO, tohum), true, false));
// (b) Pota's level 5 / 6 feature sets: a closed feature skips its step.
ekle(oyna("line-11", 11, CIZGI, tahtaKur({ ...TASARIM, ozellikler: CIZGI }, AMMO, 11), true, true));
ekle(oyna("line-bomb-12", 12, CIZGI_BOMBA, tahtaKur({ ...TASARIM, ozellikler: CIZGI_BOMBA }, AMMO, 12), true, true));
// (c) all features. Every fourth seed plays the dealt board as it is; the others get pre-placed specials and golden balls.
for (let tohum = FIRST_SEED; !yeter(); tohum++) {
  if (tohum >= FIRST_SEED + MAX_GAMES) throw new Error(`coverage not reached after ${MAX_GAMES} games: ${JSON.stringify(sayac)}`);
  const dealt = tahtaKur({ ...TASARIM, ozellikler: OZELLIK_HEPSI }, AMMO, tohum);
  const yalin = tohum % 4 === 0;
  ekle(oyna(`all-${tohum}`, tohum, OZELLIK_HEPSI, yalin ? dealt : yerlestir(dealt, new Zar(tohum)), yalin, true));
}

const design = {
  size: TASARIM.boyut, colorCount: TASARIM.renkSayisi, hpMin: TASARIM.can.alt, hpMax: TASARIM.can.ust, ammo: AMMO,
  targets: TASARIM.hedefler.map((h) => ({ color: h.renk ?? 0, remaining: h.adet })),
};
const head = JSON.stringify({ version: 1, pota: process.env.POTA_REV ?? "unknown", design, coverage: sayac, games: "@" });
const text = head.replace('"@"', "[\n" + games.map((g) => JSON.stringify(g)).join(",\n") + "\n]") + "\n";
writeFileSync(OUT, text);
const moves = games.reduce((n, g) => n + g.moves.length, 0);
const steps = games.reduce((n, g) => n + g.moves.reduce((k, m) => k + m.steps.length, 0), 0);
console.log(`special-rules.json: ${games.length} games, ${moves} moves, ${steps} steps, ${Math.round(text.length / 1024)} KB`);
console.log("coverage " + Object.entries(sayac).map(([k, v]) => `${k}=${v}`).join(" "));
