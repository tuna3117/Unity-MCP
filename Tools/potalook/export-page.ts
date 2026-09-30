// Runs Pota's own drawing and sound code (from the SCRATCH COPY; run_export.sh adds `export` to the private helpers there)
// and hands the results to export.mjs as base64 through window.__disa. Never runs inside ~/projects/Pota.
import { EtiketAtlasi } from "../src/yapbozSahne";
import { izDokusu, yildizKivilcim, yumusakDaire } from "../src/patlamaEfekt";
import { topKureDokusu } from "../src/efekt";

type Disa = Record<string, string>;
declare global { interface Window { __disa?: Promise<Disa> } }

const RATE = 48000;
const png = (c: unknown): string => (c as HTMLCanvasElement).toDataURL("image/png").split(",")[1]!;
const resim = (yol: string) => new Promise<HTMLImageElement>((ok, hata) => {
  const i = new Image(); i.onload = () => ok(i); i.onerror = () => hata(new Error(yol)); i.src = yol;
});

async function dokular(): Promise<Disa> {
  await document.fonts.load('400 48px "Lilita One"');
  if (!document.fonts.check('400 48px "Lilita One"')) throw new Error("Lilita One did not load");
  const atlas = new EtiketAtlasi();
  atlas.rakamlariCiz();   // again, now that the font is surely in (the constructor may have drawn with the fallback)
  // renkAviBolum.ts ESYALAR order; yapbozSahne.ts esyalariGiydir "cikartma", ozelleriGiydir / engelleriGiydir "payli".
  for (const [i, ad] of ["forma", "ayakkabi", "sapka", "corap", "bileklik"].entries())
    atlas.resimCiz(100 + i, await resim(`/assets/esya-${ad}.png`), "cikartma");
  for (const [i, ad] of ["cizgi", "bomba", "altin"].entries()) atlas.resimCiz(105 + i, await resim(`/assets/ozel-${ad}-a.png`), "payli");
  for (const [i, ad] of ["file", "buz", "hakem"].entries()) atlas.resimCiz(108 + i, await resim(`/assets/engel-${ad}.png`), "payli");
  return {
    "label-atlas.png": png(atlas.doku.image),
    "star.png": png(yildizKivilcim().image),
    "trail.png": png(izDokusu().image),
    "soft-disc.png": png(yumusakDaire().image),
    "soft-ring.png": png(yumusakDaire(128, 0.62, 0.98).image),
    "ball-skin.png": png(topKureDokusu().image),
  };
}

/** Seeded random (Pota uses Math.random for the noise buffer and the room impulse; a seed keeps exports reproducible). */
function rastgele(tohum: number): () => number {
  let s = tohum >>> 0;
  return () => ((s = (Math.imul(s, 1664525) + 1013904223) >>> 0) / 4294967296);
}

/** ses.ts SesMotoru's graph, offline: efektG (1) → ana (0.9) → out, plus efektG → send 0.09 → 0.28 s room convolver → ana. */
class Cevrimdisi {
  readonly c: OfflineAudioContext;
  private readonly efektG: GainNode;
  private readonly gurultuTampon: AudioBuffer;
  constructor(saniye: number, tohum: number) {
    const r = rastgele(tohum);
    this.c = new OfflineAudioContext(2, Math.ceil(RATE * saniye), RATE);
    const ana = this.c.createGain(); ana.gain.value = 0.9; ana.connect(this.c.destination);
    this.efektG = this.c.createGain(); this.efektG.connect(ana);
    const n = Math.floor(RATE * 0.28), imp = this.c.createBuffer(2, n, RATE);
    for (let k = 0; k < 2; k++) { const d = imp.getChannelData(k); for (let i = 0; i < n; i++) d[i] = (r() * 2 - 1) * Math.pow(1 - i / n, 2.6); }
    const oda = this.c.createConvolver(); oda.buffer = imp;
    const gonder = this.c.createGain(); gonder.gain.value = 0.09;
    this.efektG.connect(gonder).connect(oda).connect(ana);
    this.gurultuTampon = this.c.createBuffer(1, RATE, RATE);
    const d = this.gurultuTampon.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = r() * 2 - 1;
  }
  // ses.ts `ton`, t0 = gecikme (the context starts at 0).
  ton(f0: number, sure: number, tip: OscillatorType, kazanc: number, f1?: number, gecikme = 0, atak = 0.005): void {
    const c = this.c, t0 = gecikme;
    const o = c.createOscillator(); o.type = tip; o.frequency.setValueAtTime(f0, t0);
    if (f1) o.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t0 + sure);
    const g = c.createGain();
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(kazanc, t0 + atak); g.gain.exponentialRampToValueAtTime(0.0001, t0 + sure);
    o.connect(g).connect(this.efektG); o.start(t0); o.stop(t0 + sure + 0.02);
  }
  // ses.ts `gurultu`.
  gurultu(sure: number, tip: BiquadFilterType, q0: number, kazanc: number, q1?: number, gecikme = 0, Q = 1): void {
    const c = this.c, t0 = gecikme;
    const s = c.createBufferSource(); s.buffer = this.gurultuTampon; s.loop = true;
    const f = c.createBiquadFilter(); f.type = tip; f.Q.value = Q; f.frequency.setValueAtTime(q0, t0);
    if (q1) f.frequency.exponentialRampToValueAtTime(q1, t0 + sure);
    const g = c.createGain();
    g.gain.setValueAtTime(0.0001, t0); g.gain.exponentialRampToValueAtTime(kazanc, t0 + 0.01); g.gain.exponentialRampToValueAtTime(0.0001, t0 + sure);
    s.connect(f).connect(g).connect(this.efektG); s.start(t0); s.stop(t0 + sure + 0.02);
  }
  // ses.ts `ornekCal` without the random rate (Unity jitters the pitch 0.97–1.03 at play time).
  ornek(b: AudioBuffer): void {
    const s = this.c.createBufferSource(); s.buffer = b; s.connect(this.efektG); s.start(0);
  }
}

/** 16-bit PCM WAV, base64. */
function wav(b: AudioBuffer): string {
  const kanal = b.numberOfChannels, n = b.length, veri = n * kanal * 2;
  const v = new DataView(new ArrayBuffer(44 + veri));
  const yaz = (o: number, s: string) => { for (let i = 0; i < s.length; i++) v.setUint8(o + i, s.charCodeAt(i)); };
  yaz(0, "RIFF"); v.setUint32(4, 36 + veri, true); yaz(8, "WAVE"); yaz(12, "fmt "); v.setUint32(16, 16, true);
  v.setUint16(20, 1, true); v.setUint16(22, kanal, true); v.setUint32(24, b.sampleRate, true);
  v.setUint32(28, b.sampleRate * kanal * 2, true); v.setUint16(32, kanal * 2, true); v.setUint16(34, 16, true);
  yaz(36, "data"); v.setUint32(40, veri, true);
  const kanallar = Array.from({ length: kanal }, (_, k) => b.getChannelData(k));
  let o = 44;
  for (let i = 0; i < n; i++) for (let k = 0; k < kanal; k++) {
    const x = Math.max(-1, Math.min(1, kanallar[k]![i]!)); v.setInt16(o, x < 0 ? x * 0x8000 : x * 0x7fff, true); o += 2;
  }
  let s = ""; const u8 = new Uint8Array(v.buffer);
  for (let i = 0; i < u8.length; i += 0x8000) s += String.fromCharCode(...u8.subarray(i, i + 0x8000));
  return btoa(s);
}

async function sesler(): Promise<Disa> {
  const out: Disa = {};
  const MERDIVEN = [523, 587, 659, 784, 880, 1047, 1175, 1319, 1568, 1760, 2093];   // ses.ts SesMotoru.MERDIVEN
  for (const [d, f] of MERDIVEN.entries()) {
    const m = new Cevrimdisi(0.6, 100 + d);
    m.ton(f, 0.12, "triangle", 0.18, f * 1.25); m.ton(f * 2, 0.18, "sine", 0.1, undefined, 0.05);   // ses.ts case "kombo"
    out[`kombo-${d}.wav`] = wav(await m.c.startRendering());
  }
  for (const [i, r] of [0.92, 1.0, 1.08].entries()) {
    const m = new Cevrimdisi(0.5, 200 + i), k = Math.min(1, 0.35 + 0.8 * 0.15);                   // ses.ts case "carpma", guc 0.8
    m.gurultu(0.09, "bandpass", 900 * r, 0.3 * k, 500, 0, 3); m.ton(230 * r, 0.11, "sine", 0.2 * k, 95);
    out[`carpma-${i}.wav`] = wav(await m.c.startRendering());
  }
  const cozucu = new OfflineAudioContext(2, RATE, RATE);
  // ses.ts ORNEK: these events play their mp3 (dokun too — the synth "dokun" is only the fallback when the sample is missing).
  for (const ad of ["blok-pop", "salvo", "dustu", "dondur", "eksi", "kazandi", "kaybetti", "dokun"]) {
    const b = await cozucu.decodeAudioData(await (await fetch(`/assets/ses/${ad}.mp3`)).arrayBuffer());
    const m = new Cevrimdisi(b.duration + 0.35, 300);
    m.ornek(b);
    out[`${ad}.wav`] = wav(await m.c.startRendering());
  }
  return out;
}

window.__disa = (async () => ({ ...(await dokular()), ...(await sesler()) }))();
