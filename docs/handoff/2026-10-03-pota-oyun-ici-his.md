# Pota'ya teslim: oyun içi his katmanı (F1–F13)

_2026-10-03. Kaynak: Unity MCP atölyesi (`~/projects/Unity MCP`, dal `feel-layer`). Okuyan: Pota projesinde (`~/projects/Pota`, three.js) çalışan Claude Code oturumu ve Tuna._

Pota'nın okunmasından (basket turu, Renk Avı, menü katmanı; 2026-10-03) çıkan eksik his listesi Unity'de kuruldu ve her an için
**Önce | Sonra** videosu alındı. Ses, müzik ve titreşim bu turda yok (Tuna). Bu belge Pota'ya taşınacak anları, sayılarını ve Unity'deki
referans kodu toplar. **Kurallar ve ekonomi değişmez**; tek istisna kalan topların paraya dönüşmesi, o da Pota'nın kendi formülüyle
gösterilir (`min(20, floor(kalan × 0,1))`, `akis.ts` ODUL).

## 1. Önce bunlara bak

| Ne | Yol |
|---|---|
| Her an Önce \| Sonra, arka arkaya | `~/projects/Unity MCP/docs/screenshots/2026-10-03-feel-all.mp4` |
| Tek tek anlar | `…/2026-10-03-feel-<an>-side.mp4` ve `…-sheet.jpg`; `<an>` = intro, lowammo, finale, lose, chain, idle (Renk Avı), pull, miss, swish (basket) |
| Tasarım | `~/projects/Unity MCP/docs/superpowers/specs/2026-10-03-pota-ingame-feel-design.md` |
| Unity'de canlı | `Scenes/ColorHunt.unity` ("Yeni tahta" tam açılışı oynatır), `Scenes/Basket.unity`, `Scenes/PotaFlow.unity`; anahtar `ColorHuntSettings.Feel.Enabled`, `BasketSettings.Feel.Enabled` |

## 2. Anlar ve sayılar

Süreler gerçek saniye. Yazı tipi Lilita One, renkler HUD paleti (lacivert panel `#142850`, altın `#ffc83d`, kırmızı `#ff6b5e`).

### Renk Avı

| # | An | Ne oluyor | Sayılar | Unity kodu |
|---|---|---|---|---|
| F1 | Bölüm başı | "BÖLÜM N" şeridi üstten iner (0 → 1,15 → 1), hedef eşyaları şeridin altında çip olarak belirir (0,06 sn arayla), çipler hedef paneline **uçar** (kavis, hızlanma, varışta panel ezilir + halka), şerit yukarı çıkar, bloklar alttan üste dalga hâlinde zıplar. Dokunuş kalan kısmı 0,3 sn'ye sıkıştırır. | şerit 0,00–0,30; çipler 0,20+; uçuş 0,70 (0,35 sn, 0,05 arayla); şerit çıkışı 0,95 (0,25 sn); bloklar 0,95'ten satır başına 0,03 sn, 0,30 sn, 1,15 taşma; hazır ≤ 1,6 | `Feel/LevelIntro.cs`, `ColorHuntGame.StartIntro/SkipIntro`, `StudioCurves.IntroScale/FlightPoint`, `FeelCurves.RibbonScale` |
| F2 | Az top gerilimi | Cephane en ucuz bloğun 2 katının altındayken sayaç kırmızı nabız (1 Hz, 1,06 ölçek), ekran kenarında kırmızı nefes; parası yeten tek blok kaldıysa ipucu beklemeden kıpırdar. | kenar alfa 0,18 × nefes; nabız 1 Hz | `Feel/AmmoTension.cs`, `Studio/AmmoCounter.cs`, `FeelCurves.HintDelay` |
| F3 | Kalan topların finali | Kazanınca kalan toplar cephane panelinden tahtaya salvo gibi fırlar (6–14 uçuş), çarptığı blok patlar, içinden altın para çıkıp üst ortadaki "+N PARA" rozetine uçar, rozet yuvarlanarak artar, panel sayacı her kalkışta düşer; son parada rozet 1,3 taşarak parlar; sonra tahta dağılır (bugünkü) ve kart. | uçuş sayısı: kalan ≤ 6 → kalan, yoksa 6–14; aralık 0,08 sn (toplam ≤ 2,0 sn olacak şekilde kısalır); top 0,22 sn, para 0,45 sn; para = `min(20, floor(kalan×0,1))`, para 0 ise rozet yok | `Feel/LeftoverFinale.cs`, `FeelCurves.FinalePlan/LeftoverCoins`, `ColorHuntGame.BurstCell/RandomLiveCell`; para mesh'i `vfx-coin` |
| F4 | Zincir kelimeleri | Derinlik 3 "Süper!", 4 "Harika!", 5+ "Muhteşem!" (×d rakamı kalır); 5+ zincirde 0,25 sn ×0,5 yavaş çekim + konfeti. | kelime 0,6 sn, 30 css yükselir | `Bursts/PotaBurstStyle.ChainPeak`, `Bursts/CandyHud.Word` |
| F5 | Boşta ipucu | 5 sn dokunulmazsa en iyi blok ±4° kıpırdar ve parlar; puan = zincir adımı + topladığı hedef eşya sayısı (hedef renk eşit adımda öne geçer). | 5 sn, 0,6 sn kıpırtı | `ColorHuntHint.Score/Best`, `ColorHuntGame.TickHint` |
| F6 | İniş yaylanması | Blok düşünce (x 1,08, y 0,86) → y 1,04 taşma → (1, 1); tekdüze 0,88 küçülme yerine. | 0,18 sn | `BlockView.Bounce`, `FeelCurves.LandBounce` |
| F7 | Dolum maskesi | Yeni bloklar çerçevenin üstünde görünmez: tahta katmanı yalnız çerçeve içinde çizilir (stencil). three.js karşılığı: bloklara `clippingPlanes` (çerçeve üst kenarı) ya da çerçeve içi stencil. | maske üst kenarı = çerçeve iç kenarı; alt tarafa 20 m uzanır (altın top düşüşü görünsün) | `Feel/BoardClip.cs`, `Shaders/PotaStencilMask.shader`, `Editor/BoardClipInstaller.cs` |
| F8 | Canlı arka plan | 6 toon bulut 92 m yarıçaplı halkada 0,5 m/sn kayar (+ ±0,4 m salınım); 5 kuşluk V sürüsü 22 sn'de bir 9 sn'de geçer (kanat 0,5 sn döngü); çatının ardında 180° düşük poligon şehir bandı (duvar + cam). | bulut ölçek 1,6 (×0,8–1,2), yükseklik +18/+22/+26; kuş ölçek 5, yükseklik +14, derinlik +42, −32…+32 m | `Flow/SkyLife.cs`, `FeelCurves.CloudPose/BirdPass`; Blender tarifleri `recipes/{cloud_set,bird_flock,city_band}.py`, glb'ler `Art/Props/` |
| F9 | Kaybetme anı | "Cephane bitti" ile tahta %60 parlaklığa griye kayar (0,4 sn), kamera 0,3 m geri; bitmemiş hedef satırları kırmızı nabız + altında "N kaldı"; toplam kalan ≤ 3 ise ortada sarsılan "Kıl payı!"; kart 1,2 sn sonra. | dim 0,6 / 0,4 sn; geri 0,3 m; kart 1,2 sn | `Feel/LoseMoment.cs`, `BlockView.Fade`, `ColorHuntCamera.BackOffset` |

### Basket

| # | An | Ne oluyor | Sayılar | Unity kodu |
|---|---|---|---|---|
| F10 | Sapan | Nişan boyunca kahraman elinde top tutar; çektikçe geriye yaslanır (omurga −12°'ye kadar), kalça 6 cm çöker; yörünge kalınlaşır ve parlar (0,06 → 0,085 m, alfa 0,6 → 1), tam güçte iniş halkası 1 Hz nabız; nişanın düştüğü üst çember altına parlar (0,2 sn); kamera 0,4 m geri ve 2° aşağı. Bırakınca / atışta hepsi 0,15 sn'de döner. | eğim ağırlığı smoothstep(çekme) | `Basket/Feel/SlingshotFeel.cs`, `PlayerActor.SetLean/HandBall`, `Hoop.SetGlow`, `AimPreview.SetPower`, `RoundCamera.PullOffset/PullPitch` |
| F11 | Iska | "KAÇTI!" levhası (Blender `lettering` miss stili) potaların üstünde 0,12 sn'de 1,2 taşarak oturur, ±4° sallanır, 1,0 sn; üst çemberler 0,3 sn kırmızı yanıp söner (3 kez); kahraman 8° öne çöker (0,4 sn); kamera 0,15 sn'de 0,2 m çöker, 0,5 sn'de döner. HUD'daki eski "Kaçtı" yazısı kalkar. | — | `Basket/Feel/MissFeel.cs`, `Hoop.Blink`, `PlayerActor.SetSlump`, `RoundCamera.SagOffset`; levha `Art/VFX/vfx-letter-miss/` |
| F12 | Temiz giriş | Top üst çembere **değmeden** geçerse "SWISH!" levhası çemberin 0,75 m üstünde (1,5 m geniş, 0,9 sn) + altın swish patlaması; çembere değip girince bugünkü geri bildirim. | top başına "çembere değdi" bayrağı, her atışta sıfırlanır | `Basket/Feel/SwishFeel.cs`, `Ball.TouchedRim`, `Hoop.OnRimContact` |
| F13 | Sayım ve damga | Sepet sayacı eski rakamı yukarı kaydırıp yenisini getirir (0,14 sn); zıplama inişler sıklaştıkça 0,25 → 0,10'a iner; tur bitince "N TOP" rozeti kafesin üstünde 1 → 2,2 → 1,8 damgalanır (0,35 sn), 1,5 sn durur. | — | `Basket/Feel/BasketCountFeel.cs`, `BasketHud` (Roll/PopAmount), `Flow/FlowBadge`, `FeelCurves.Stamp/CountPop` |

## 3. Varlıklar (Blender atölyesi, `~/projects/Blender MCP`)

| Varlık | Tarif | Teslim |
|---|---|---|
| KAÇTI! levhası | `vfx_flipbook lettering params {text: "KAÇTI!", style: "miss"}` 512×256 | `assets/Pota/vfx-letter-miss/export/` |
| Bulut seti (3) | `recipes/cloud_set.py` (`make`), ≤ 400 üçgen/bulut | `assets/Pota/cloud-set/export/cloud-set.glb` |
| Kuş sürüsü (5, `flap` klibi) | `recipes/bird_flock.py` | `assets/Pota/bird-flock/export/bird-flock.glb` |
| Şehir bandı | `recipes/city_band.py` (28 bina + pencere şeritleri, 1 668 üçgen) | `assets/Pota/city-band/export/city-band.glb` |
| Altın para | `vfx_mesh coin` (zaten vardı) | `assets/Pota/vfx-coin/export/` |

three.js'e glb'ler doğrudan yüklenir (glTF, +Y yukarı, metre). Bulutlar/kuşlar için Pota'da `MeshToonMaterial` ya da düz renk yeter.

## 4. Pota'da bulunan iki hata (okuma sırasında)

- `kazandi` sesi ve başarı titreşimi iki kez: `renkAviFaz.ts:940` (`sonuc`) ve `akis.ts:1095` (`fazBBitti`), 0,85 sn arayla. `kaybetti` de aynı (`akis.ts:1099`, 0,45 sn). Birini kaldır.
- Kazanma penceresindeki lig çubuğu (`ligCubukYaz`) genişliği pencere gizliyken yazılıyor; CSS geçişi (0,6 sn) muhtemelen hiç oynamıyor. Telefonda bakılmalı.

## 5. Tuna'nın seçimi

Tuna videolara bakıp hangi anların Pota'ya gireceğini seçer; bu tablo o zaman doldurulur.

| # | An | Karar | Not |
|---|---|---|---|
| F1 | Bölüm başı | | |
| F2 | Az top gerilimi | | |
| F3 | Kalan topların finali | | |
| F4 | Zincir kelimeleri | | |
| F5 | Boşta ipucu | | |
| F6 | İniş yaylanması | | |
| F7 | Dolum maskesi | | |
| F8 | Canlı arka plan | | |
| F9 | Kaybetme anı | | |
| F10 | Sapan | | |
| F11 | Iska | | |
| F12 | Temiz giriş | | |
| F13 | Sayım ve damga | | |

## 6. Bu turda yapılmayanlar

- Ses, müzik, titreşim (Tuna: bu turda yok).
- Menü ve ödül katmanı (kazanma/kaybetme pencereleri, harita ilerlemesi, ana ekran, market, öğretici eli): 2. alt proje, ayrı belge.
- Pota sütunlu karşılaştırma videosu: Pota kaydı Chromium'u öne alır (Tuna aynı Mac'te çalışıyor); Tuna uygun bir saatte isterse
  `Tools/potalook/record-pota.mjs` ile eklenir.
