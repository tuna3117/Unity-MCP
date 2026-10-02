# PotaFlow: üç yeni geçiş adayı — tasarım (2026-10-02)

Tuna'nın isteği (2026-10-02): "Basketten puzzle'a geçerkenki görüntü için 3 alternatif ver; aynı asset'leri kullan, gerekirse üret,
benzer olsun ki anlayalım." Sohbette sunulan üç aday ve üretim planı onaylandı ("onay").

Önceki tur: `2026-09-29-pota-flow-transition-design.md` (A "Akan toplar" seçildi, toplar kahramanın sepetine; B "Sade" kodda).
Bu tur A'nın üstüne kurulur: **ilk ~1,45 sn üç adayda da A ile aynı** (kafes vurgusu, toplar sepete, kahraman görünür ve sepete döner,
kamera yan manzaraya), fark sepet dolduktan sonra başlar. Böylece karşılaştırma adil olur ve aynı varlıklar kullanılır.

## 1. Kararlar

| # | Karar |
|---|---|
| 1 | Üç aday `FlowCandidate` olarak A ve B'nin yanına eklenir: `BigShot` (1 "Büyük şut"), `Dive` (2 "Çemberden dalış"), `Stamp` (3 "Pano damgası"). A varsayılan kalır; seçim `PotaFlowSettings.Candidate`. |
| 2 | Yeni Blender varlığı yok. Kullanılanlar: toon kahraman + `player_shoot` klibi, `BallBin`, `BallStream`, `GiantHoop` (file, sallanma), Renk Avı Pota görünümü (bloklar, düşüş ezilmesi), Blender flipbook'ları `SpeedLines`, `LetterSwish`, `CandyPuff` (puff_toon), `StarSprite`, Court'un halka dokusu. |
| 3 | Zaman çizelgesi saf kod (`FlowTimeline`), her adayın sayıları `FlowTiming` içinde kendi bloğunda; kamera yolları `CameraFlight` (N noktalı Catmull-Rom ve Pota tarzı tek kayma eklenir). |
| 4 | Hazır süreleri: 1 ≈ 3,3 sn, 2 ≈ 3,1 sn, 3 ≈ 2,9 sn (A 2,5). Dokunma atlaması aynen çalışır (≤ 0,35 sn). |
| 5 | Teslim: `ai_potaflow_record` ile üç kayıt + A + Pota (scratch kopyasından, güncel main) → `docs/screenshots/2026-10-02-potaflow-candidates-{sheet.jpg,side.mp4}` (Pota \| A \| 1 \| 2 \| 3). Tuna seçer. |
| 6 | U26 (PotaFlow sahası Pota'nın çatısı değil) için iki kare: mevcut saha ve `BuildRoof` açık hali; Tuna'ya ayrıca gösterilir. |

## 2. Ortak ilk bölüm (A'dan, değişmez)

0–0,30 vurgu; 0,25–0,77 toplar kafesten; 0,30–1,10 kamera yan manzaraya (kahraman + sepet); 0,60 kahraman görünür; 0,90–1,25
kahraman sepete döner; 0,95–1,45 toplar sepete iner. Cephane paneli ve sayım adaya göre (aşağıda).

## 3. Adaylar (gerçek zaman, sn)

### 1 — "Büyük şut" (`BigShot`)

| t | Olay |
|---|---|
| 1,45–1,65 | Kahraman sepetten dev potaya döner (+z). 1,55'te `Shoot` klibi (atış klibinin salınımı). |
| 1,50–2,05 | Kamera manzaradan **atış pozuna**: kahramanın arkasından ve biraz yandan, alt üçte bir kahraman, üst yarı dev pota (Pota `saha` pozunun uyarlaması; kule yüzünün önünde kalır). |
| 1,65–2,00 | Sepetteki toplar (en çok 12, sepetteki kadar) 0,03 sn arayla sepetten fışkırır; `BallStream` ikinci geçiş, kaynak = sepet yuvası, hedef = `GiantHoop` (file). Sepetteki duran toplar fırlayana kadar yerinde bekler. |
| 2,45–2,85 | Toplar çemberden geçip fileye düşer (en çok 10 yığılır), file şişer, her inişte `land`; **cephane sayacı yalnız bu inişlerle artar** (paylar toplamı N). Rozet "N TOP" ilk inişte panele uçar. Cephane paneli 2,30'da girer. |
| 2,55–3,05 | Kamera atış pozundan tahta pozuna (ileri itiş, `LookLead`). |
| 2,60– | Tahta halka halka belirir, çerçeve parlar; 2,85–3,20 HUD. |
| 3,30 | Hazır. |

### 2 — "Çemberden dalış" (`Dive`)

Toplar sepette kalır, sayım A gibi (0,95–1,45). Kameranın kendisi top olur.

| t | Olay |
|---|---|
| 1,50–2,10 | **Kovalama:** kamera manzaradan alçalıp sahanın üstünden dev potaya doğru hızlanır (FOV 46 → 70), ekran kenarlarında hız çizgileri (`SpeedLines` flipbook, 8 kare döngü, kamera önünde quad). |
| 2,10–2,32 | **Çemberden geçiş:** kamera çemberin altından filenin halkaları arasından yukarı çıkar (yol tam çember merkezinden), 2,20'de `swish` sesi, çemberde swish halkası (Court halka dokusu 0,5 → 0,9 R), file sallanır (`GiantHoop.Swing`), çemberin üstünde "SWISH!" yazısı (`LetterSwish`, 0,5 sn pop). |
| 2,32–2,90 | **Geri çekilme:** kamera çemberin üstünden geriye ve yukarı tahta pozuna süzülür (FOV 70 → 38); bu sırada 2,40'tan tahta halka halka belirir (kamera uzaklaşırken bloklar "pat pat" dolar), çerçeve parlar; 2,65–3,00 HUD. |
| 3,10 | Hazır. |

Kamera yolu N noktalı Catmull-Rom: manzara → saha üstü alçak (0; 1,6; 20) → çember altı (0; 1,3; 27,8) → çember üstü (0; 4,6; 27,8) →
tahta pozu. Bakış noktaları: kovalamada çember, geçişte yukarı/ileri, sonra tahta merkezi. Çemberin halka tüpü (0,1) merkezden geçen
kamerayı kesmez (R 1,6, near 0,1). U11'in boş gökyüzü karesi bu adayda yok (bakış hep potada).

### 3 — "Pano damgası" (`Stamp`)

Sayım A gibi ama panel sonda girer: rozet sepetin üstünde bekler, kamera ile gider, cama damgalanır, panele uçar.

| t | Olay |
|---|---|
| 1,45–2,15 | Kamera manzaradan tahta pozuna **tek kayma**, ortada 3,5 m yükselme (Pota `pozKaris`: smoothstep + sin(πu)·yükselme), FOV 46 → 38. |
| 1,45–2,15 | Rozet "N TOP" ekranda yukarı doğru büyüyerek yol alır (B'nin rozet sürüşü). |
| 2,15 | **Damga:** rozet cam merkezine çarpar (ölçek 1 → 2,2 → 1,8, 0,18 sn), camda puff (`CandyPuff` 16 kare, 0,35 sn) + yıldız flaşı, `multiply` sesi, çerçeve parlar 0,4 sn, kamera kısa itiş-oturuş. |
| 2,20–2,60 | **Blok yağmuru:** her sütunun blokları çerçevenin üstünden (n+1 hücre yukarıdan) sabit ivmeyle düşer, sütunlar 0,04 sn arayla soldan sağa; inişte ezilme (`LandSquash`), çerçeve parlar. |
| 2,45–2,70 | Rozet cephane paneline uçar, panel 2,40'ta girer, varışta `PopAmmo`; hedef paneli/oklar 2,55–2,90. |
| 2,90 | Hazır. |

## 4. Kod

- `FlowCandidate` + 3 değer; `FlowTiming` içine `BigShotTiming`, `DiveTiming`, `StampTiming` blokları; `FlowTimeline`: `Ready`, `CountAt`,
  `CameraProgress`/yol seçimi, `RevealStart`, `AmmoProgress`, `HudProgress`, `BadgeFly` adaya göre; yeni saf fonksiyonlar: ikinci top
  programı (`BallSchedule`), `StampScale(t)`, `RainOffset(t, col)`, `DiveFov(t)`, `SpeedLinesAlpha(t)`, `SwishPop(t)`.
- `BallStream.Begin(IBallSchedule, Func<int,Vector3> source, IBallReceiver, float holdFrom)`: top, fırlayana kadar kaynağında bekler.
- `CameraFlight.Path(CamPose[] pts, u)` (N noktalı), `CameraFlight.Sweep(a, b, u, rise)` (Pota).
- `PlayerActor.PlayShoot()`; `BoardReveal.ApplyRain(game, tl, t)` (Pos.y ofseti + Squash).
- `PotaFlowDirector`: adaya göre kamera yolu, ikinci akış, dalış efektleri (hız çizgisi quad'ı, swish halkası, SWISH! yazısı), damga
  efektleri (puff + yıldız), rozet yerleşimi. Yeni efektler `Game.S` (ColorHuntSettings) dokularından, `RuntimeMaterial.From` şablonlarıyla.
- `ai_potaflow_record` `candidate` parametresi `A|B|1|2|3`.
- Testler: `FlowTimelineTests` (her aday: sayaç N'de biter, hiç düşmez; ikinci program sepet kadar top ve N toplamı; damga ve yağmur
  eğrileri 0→1; dalış FOV uçlarda tam); `CameraFlightTests` (N noktalı yol uçlarda tam ve noktalardan geçer; Sweep ortada yükselir);
  PlayMode: her aday hatasız oynanabilir tahtada biter, dokunma atlaması çalışır.

## 5. Teslim ve ölçü

Pota | A | 1 | 2 | 3 yan yana levha (0,2 / 0,8 / 1,4 / 1,9 / 2,4 / 2,9 sn) ve sessiz video; her adayın 2–3 yakın kare. Dürüst notlar:
Blender yeni varlık üretmedi; adaylar A'dan 0,4–0,8 sn uzun; U26 karesi.
