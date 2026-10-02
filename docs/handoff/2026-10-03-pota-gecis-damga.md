# Pota'ya teslim: basket → bulmaca geçişi, 3 "Pano damgası"

_2026-10-03. Kaynak: Unity MCP atölyesi (`~/projects/Unity MCP`). Okuyan: Pota projesinde (`~/projects/Pota`, three.js) çalışan Claude Code oturumu ve Tuna._

Unity atölyesinde basket turundan bulmaca tahtasına geçiş için üç aday yapıldı ve Tuna'ya Pota'nın bugünkü geçişiyle yan yana
gösterildi. **Tuna 3 "Pano damgası"nı seçti** (2026-10-02). Bu belge o geçişi Pota'da kurmak için gereken her şeyi toplar: ne görünüyor,
saniye saniye ne oluyor, sayılar, varlıklar, sesler ve Unity'deki çalışan referans kod.

**Kurallar ve ekonomi değişmez.** Cephane bugünkü gibi sepet sayısıdır (`akis.ts` `fazABitti` → `fazBBasla`). Tahta kuralları, zorluk,
kalibrasyon, öğretici dokunulmaz. Değişen yalnız **iki faz arasındaki görüntü**: kamera, kahraman, toplar, "N top" yazısı, tahtanın
belirişi ve HUD'un girişi. Pota'nın kendi görünümü (bloklar, HUD, çatı) kalır; Unity'den yalnız koreografi ve efektler alınır
(Tuna'nın 2026-10-02 notu: "kareler Unity'de bozuk ama efektler on numara; oyunu bozma").

## 1. Önce bunlara bak

| Ne | Yol |
|---|---|
| Yan yana video: Pota (bugün) \| A \| 1 \| 2 \| **3** | `~/projects/Unity MCP/docs/screenshots/2026-10-02-potaflow-candidates-side.mp4` (3 en sağ sütun) |
| Aynı anların kare levhası | `~/projects/Unity MCP/docs/screenshots/2026-10-02-potaflow-candidates-sheet.jpg` (satır "3 Damga") |
| Top boyu önce/sonra (toplar gerçek boyda olmalı) | `~/projects/Unity MCP/docs/screenshots/2026-10-02-potaflow-ball-size-sheet.jpg` |
| Tasarım (spec) §3.3 | `~/projects/Unity MCP/docs/superpowers/specs/2026-10-02-potaflow-candidates-design.md` |
| Önceki tur (A: toplar sepete; ilk 1,45 sn'nin kaynağı) | `~/projects/Unity MCP/docs/superpowers/specs/2026-09-29-pota-flow-transition-design.md`, video `docs/screenshots/2026-09-30-potaflow-basket-side.mp4` |
| Unity'de canlı deneme | Unity MCP projesi, `Assets/_Project/Games/Pota/Scenes/PotaFlow.unity` → Play, yukarı sürükleyerek at; tur bitince geçiş (varsayılan `Stamp`) |

## 2. Ne görünüyor (tek cümleyle)

Sepet dolunca kafes ağzında "N TOP" zıplar, toplar kafesten fışkırıp **kahramanın yanındaki tel sepete** dolar (kamera o sırada
yandan kahramana ve sepete bakar), sonra kamera **Pota'nın bugünkü tek kaymasıyla** tahtaya gider; "N TOP" yazısı kamerayla gelir ve
**buzlu cama damgalanır** (puf + yıldız flaşı, çerçeve parlar), bloklar **sütun sütun yukarıdan yağar**, cephane paneli girer, yazı
panele uçar, tahta hazır.

## 3. Zaman çizelgesi (gerçek saniye, t = 0 sepet dolduğu an)

Unity'de her şey saf bir "t'de ne doğru?" fonksiyonundan sürülür (`FlowTimeline`), bu yüzden dokunma atlaması ve düşen kare bozmaz.
Pota'da da öneri aynı: tek bir `t` (gerçek zaman, `timeScale`'den bağımsız) ve her parça için `t`'den hesaplanan durum.

### 3a. Ortak giriş (A'dan, 0–1,45)

| t | Olay | Sayılar |
|---|---|---|
| 0,00–0,30 | **Vurgu.** Kamera sütunun bitiş pozunda kalır; kafes ağzında "N TOP" (ekran uzayı) 0 → 1,25 → 1 ölçekle zıplar; kafes kenarı parlar; `swish` sesi; FOV 3° içeri vuruş (sinüs). | pop: ilk %60'ta ease-out 1,25'e, kalan %40'ta 1'e |
| 0,25–0,82 | **Fışkırma.** 27 görünür top (N < 27 ise N tanesi) kafes ağzından 0,022 sn arayla çıkar. Top yarıçapı **0,12 = turun kendi topu** (0,19 kullanmayın: Tuna "neden kocaman" dedi). Kaynak kafes ağzı, x ± 0,6 / z ± 0,3 saçılma. | yol: kübik Bézier, bkz. §4 |
| 0,30–1,10 | **Kamera 1. vuruş.** Bitiş pozundan **yan manzaraya** (kahramanın yanı başından, kahraman + sepet kadrajda, dev pota arkada). Catmull-Rom, smootherstep. Unity'de kahraman (0; 0; 6,8) iken göz (8,5; 4; 4,5), bakış (0,7; 1; 7,15), FOV 46 → Pota'da kahramana göre: göz = kahraman + (8,5 yana; 4 yukarı; 2,3 kuleye doğru), bakış = kahramanla sepetin arası, bel hizası. | |
| 0,60 | Kahraman, teras ve sepet görünür (atıştan beri gizliyse). | |
| 0,90–1,25 | Kahraman **sepete döner** (smootherstep slerp). | |
| 0,95–1,45 | **İnişler.** Toplar sepete yukarıdan eğik girer (ilk iniş 0,95, son 1,45; aradakiler eşit aralıklı), sepette 3 × 3 × 3 = 27 yuvaya yığılır; her inişte sepet hafif ezilir (x,z +4 %, y −6 %, 6/sn söner) ve `land` sesi (perde 1 + 0,01·i, en çok 1,25). Cephane payı: i. top ⌊(i+1)N/27⌋ − ⌊iN/27⌋ (toplam tam N) — **3'te sayaç bu anda ekranda değil**, panel sonda girer ama değer hazırdır. | |
| 0,30–0,80 | "N TOP" rozeti kafes ağzından ekranın ortasına (0,5; 0,62) süzülür (smootherstep), ölçek 1 → 1,15 + 0,12·sin(π·kamera ilerlemesi). | 3'te rozet erken uçmaz |

**Sepet** (`BallBin`): kulenin kafesiyle aynı tel görünüm, bel hizası: 0,92 × 0,72 × 0,92 m, taban 0,10 yukarıda; kahramanın
yanında kamera tarafında (Unity: kahraman + (1,4; 0; 0,7)). Yuvalar: 3 kat, her kat 3 × 3, aralık 2,05 r, katlar 1,9 r üst üste,
1. kat +0,04 / 2. kat −0,035 kaydırma (ızgara gibi görünmesin). Pota'da sepet yok: three.js kutularla kur (Unity `CageMeshBuilder` tel
aralığı 0,2, tel 0,028, kenar 0,07/0,12) ya da Blender atölyesinden `prop` olarak iste.

### 3b. Damga (1,45–2,90)

| t | Olay | Sayılar |
|---|---|---|
| 1,45–2,15 | **Tek kayma** manzaradan tahta pozuna: **Pota'nın `kameraYonetmeni.pozKaris`'i** (smoothstep, ortada sin(πu)·3,5 m yükselme, FOV karışımı). Unity bunu birebir kopyaladı; Pota'da zaten var, yalnız başlangıç pozu manzara, süre 0,70. FOV 46 → tahta FOV'u (38). | `CameraFlight.Sweep` |
| 1,45–2,15 | Rozet kamerayla gider: ekran konumu (0,5; 0,62)'den **cam merkezinin ekran izdüşümüne** lerp (u = kayma ilerlemesi), ölçek 1,15 + 0,12·sin(πu). | |
| 2,15 | **Damga.** Rozet cam merkezinde: ölçek 1 → **2,2** (0,18 sn'nin ilk %40'ı, ease-out) → **1,8** (kalan %60), sonra 1,8'de bekler. Aynı anda: 6 **toon puf** (flipbook `vfx-puff-toon` 4×4 kare, 0,35 sn; cam merkezi etrafında 60°'de bir, yarıçap 0,22·4,5 m, boy 2,5–3,4 m, dışa hız 0,6·4,5 m/sn, her biri farklı dönüşle) + **yıldız flaşı** (Pota'nın `star.png`'si, additive, 5,9 m, 0,28 sn, sıcak beyaz (1; 0,95; 0,75), boy 0,4 → 1 → 0,7, alfa 1 − u²); **çerçeve parlar** 0,40 sn (sinüs, HDR emisyon → bloom); kamera 0,12 sn ileri **itiş-oturuş** 0,35 m; sarsıntı 0,25; `multiply` sesi (Pota karşılığı: kombo/çarpma ailesinden kısa bir vuruş). | puf ve yıldız **geçiş saatiyle** ilerletilmeli (bkz. §6) |
| 2,20–2,60 | **Blok yağmuru.** Sütunlar soldan sağa **0,04 sn** arayla başlar; bir sütunun bütün blokları başlayana kadar **gizli** ve evlerinin **(n + 1) hücre üstünde** durur, başlayınca **sabit ivmeyle 0,40 sn'de** eve iner: ofset = (n + 1)·(1 − u²). İnişte Pota'nın kendi **ezilmesi** (`LandSquash` 0,12, 6/sn söner). Son sütun 2,20 + 6·0,04 + 0,40 = 2,84'te oturur. | 7×7'de 8 hücre yukarıdan |
| 2,40–2,70 | **Cephane paneli** soldan girer (0,30 sn, ease-out). Değeri N (sayım sepet inişlerinde bitti). | |
| 2,45–2,70 | **Rozet panele uçar** (smootherstep), ölçek ×0,45'e küçülür, son %30'da söner; varışta panelin sayısı **pop** yapar (index.html `@keyframes pop`), `add` sesi. | |
| 2,55–2,90 | Hedef paneli üstten, (varsa) düğme şeridi alttan girer (0,35 sn). İpucu yazısı geçişte gizli. | |
| 2,90 | **Hazır.** Girdi açılır; **parmak hâlâ basılıysa kalkana kadar** tahta dokunuş almaz. | |

**Dokunma atlaması:** geçiş sırasında herhangi bir dokunuş kalan kısmı hızlandırır: hız = max(6×, kalan süre / 0,35 sn). Atlamada
hiçbir olay atlanmaz, yalnız hızlanır (t ilerler, her parça t'den okunur).

## 4. Top yolu ve formüller (Unity `BallStream.PathPoint`, `FlowTimeline`)

- Kübik Bézier a → b: p1 = a + (0; (apexY − a.y)·4/3; 0) + kick, p2 = b + (0; (apexY − b.y)·4/3; 0) − yatay yön · endLean.
  apexY = max(a.y, b.y) + 5 + 1,6·rastgele(0..1) (deterministik hash: her tekrar aynı görünür). kick = (0; 0; 0,8) kuleden ileri,
  endLean = 2,5 (sepete eğik girer). İnişten sonra 0,15 sn'de yuvasına kayar (d² ease). Top kendi ekseninde 540°/sn döner; kısa iz
  (0,12 sn, turuncu (1; 0,62; 0,25) α 0,45 → 0, genişlik 0,9 r).
- Çıkış zamanı i: 0,25 + 0,022·i; iniş zamanı i: 0,95 + i·(1,45 − 0,95)/26 (27 top varmış gibi aralık; az topta da ilk toplar erken iner).
- Pay: ⌊(i+1)·N/V⌋ − ⌊i·N/V⌋ (V = görünür top), toplam tam N.
- Smootherstep: u³(u(6u − 15) + 10). Vurgu pop: u < 0,6 → 1,25·(1 − (1 − u/0,6)²), sonra 1,25 → 1 doğrusal.
- Damga ölçeği: u = (t − 2,15)/0,18; u < 0,4 → lerp(1; 2,2; 1 − (1 − u/0,4)²); sonra lerp(2,2; 1,8; (u − 0,4)/0,6); u ≥ 1 → 1,8.
- Yağmur: sütun c başlangıcı 2,20 + 0,04·c; u = clamp((t − başlangıç)/0,40); ofset = (n + 1)(1 − u²); ezilme = max(0; 1 − (t − iniş)·6).

## 5. Varlıklar ve sesler

| Ne | Nerede | Not |
|---|---|---|
| Toon puf flipbook | `~/projects/Blender MCP/assets/Pota/vfx-puff-toon/export/vfx-puff-toon.png` (+ `manifest.json`: 4×4, 16 kare, 0,35 sn) | Unity kopyası `Assets/_Project/Games/Pota/Art/VFX/vfx-puff-toon/`. Şeffaf film, sRGB; Unity'de beyaz tint, additive değil (alpha blend) |
| Yıldız | Pota'nın kendi `star.png`'si (Unity'ye oradan geldi) | additive, 2× şiddet |
| Sepet (tel kafes) | Pota'da yok; Unity `Scripts/Flow/BallBin.cs` + `Basket/Visual/CageMeshBuilder.cs` (kodla kutular) | İstenirse Blender'dan `prop` teslimi (studyo.md teslim sözleşmesi) |
| Top | turun kendi topu, r 0,12, basketbol dokusu | |
| Sesler (Unity `SynthSfx`) | `swish` (vurgu), `shot` (her 3 fışkırmada bir, perde 1,15, ses 0,35), `land` (her iniş, perde 1 + 0,01 i), `multiply` (damga), `add` (rozet panele varınca) | Pota'nın kendi örnekleriyle eşle: iniş için top sesi, damga için `carpma`/`kombo` ailesinden kısa bir vuruş, varış için `topla`/`tamam` |

## 6. Unity'den çıkan dersler (Pota'da da geçerli)

- **Her şey t'den.** Kamera, toplar, rozet, yağmur, HUD: hepsi `t`'nin fonksiyonu; dokunma atlaması ve kare düşmesi senkron bozmaz.
  Sepete iniş olayları "önceki t ile şimdiki t arasında eşik geçildi mi" ile üretilir (bir olay bir kez).
- **Tahta geçişten önce kurulur ama gizli** (Pota'da zaten "hazır bekler"): blok ölçeği 0 değil 0,001 (sıfır matris sorunu),
  yağmur başlayana kadar ev konumunun (n + 1) hücre üstünde.
- **Rozet ekran uzayında, cam merkezi dünya noktasının izdüşümüyle** yerleşir; damga anında kamera hareketsizdir (kayma 2,15'te biter),
  yazı kayma sonunda tam camın ortasında kalır.
- **Puf ve yıldız geçiş saatiyle ilerletilmeli:** Unity'de kare kare kayıtta parçacık zamanı geçişin saatini izlemedi (0,35 sn'lik puf
  ~0,8 sn göründü); efekt `Pause` + her karede `Simulate(dt)` ile yönetmenin dt'siyle sürüldü. three.js'te kendi `t`'nizle sürün.
- **Parmak basılıyken tahta açılmaz:** atlamak için dokunan parmak kalkana kadar bulmaca girdisi kapalı (ilk dokunuş bir blok seçmesin).
- **İlk kare takılması:** blok görselleri geçişin ilk karesinde üretilmesin; tur sırasında bir kez önceden ısıtılır.
- **Süre:** 2,90 sn (A 2,50, Pota bugün 1,40). Tuna "istersen sıkıştırırım" teklifine henüz yanıt vermedi; Pota'da önce birebir kur,
  sıkıştırma ayrı adım (öneri: iniş penceresi 0,95–1,45 → 0,85–1,25 ve kayma 0,60).
- **Pota'nın yerleşimi farklı:** Unity'de kule orijinde, teras z 6–8,4, saha +z'de, dev pota (0; 8,5; 30). Pota'da kahraman (0; 0; 3),
  sütun sağda (14,3; 0; 0), dev pota (0; 8,5; −10) (`sahaYerlesim.ts`). Sayıları **kahramana göre** uygulayın (manzara, sepet konumu);
  mutlak koordinatları kopyalamayın. Kahraman sütun terasında duruyorsa sepet onun yanına, kamera tarafına.

## 7. Unity referans kodu (`~/projects/Unity MCP/Assets/_Project/Games/Pota/Scripts/`)

| Dosya | Ne için |
|---|---|
| `Flow/FlowTimeline.cs` | zaman çizelgesi: `StampTiming` bloğu, `StampCamera`, `StampScale`, `Stamped`, `RainStart/Progress/Offset/Visible/Squash`, `BadgeDock/BadgeFly`, `CountAt`, `Share`, `HighlightScale`, `SkipSpeed` |
| `Flow/CameraFlight.cs` | `Sweep` (= Pota `pozKaris`), `Evaluate` (ilk vuruş Catmull-Rom), `Settle` (itiş-oturuş) |
| `Flow/BallStream.cs` | top yolu (`PathPoint`), iz, iniş olayları |
| `Flow/BallBin.cs` | sepet ölçüleri, yuvalar, ezilme |
| `Flow/BoardReveal.cs` | `ApplyRain` (yağmur), `Finish` |
| `Flow/FlowEffects.cs` | `Stamp` (puf + yıldız), `Advance` (parçacıkları t ile sürme) |
| `Flow/PotaFlowDirector.cs` | sahneleme: `ApplyTransition`, `PlaceBadge` (rozetin cam izdüşümü), `CameraPose`, `ApplyEffects`, `Skip`, girdi kapıları |
| `../Settings/PotaFlowSettings.asset` | bütün sayıların kayıtlı hali (`Timing.Stamp`, `BallRadius` 0,12, `MidPos/MidLook/MidFov`, `BinPosition`, `StampPuffSize` 4,5) |
| `../Tests/EditMode/FlowTimelineTests.cs` → `Stamp_Sweep_Stamp_Rain_ThenTheBadgeFlies` | eğrilerin uçlarda ne vermesi gerektiği (Pota'da node testi olarak aynen yazılabilir) |

## 8. Kapsam dışı / açık

- Pota'ya **taşınmayacaklar:** Unity'nin blok/HUD görünümü, 28 m saha (U26 Tuna'da), A/1/2 adayları.
- **Açık sorular (Tuna):** süre sıkıştırma (§6), sepetin Blender'dan mı kodla mı geleceği, damga sesi hangi örnek.
- Pota klasörüne Unity oturumu hiçbir şey yazmaz; bu belge ve videolar Unity MCP'de durur.
