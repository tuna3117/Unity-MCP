# Pota (Unity denemeleri)

Pota / "Swish & Smash" asıl olarak three.js ile yazılmış bir iOS oyunu (`~/projects/Pota`, salt okunur, kurallar ve görseller oradan). Bu klasör, oyunun bölümlerinin Unity'deki yeniden yapımlarını tutar. Pota'nın Unity'ye geçip geçmeyeceğine Tuna karar verecek. Geçerse bu klasör olduğu gibi yeni bir Pota Unity projesine taşınır (`.meta` dosyalarıyla birlikte kopyala, GUID'ler korunur). O projede `Core/` da gerekir.

Pota üzerinde çalışırken bu dosyayı oku ve güncel tut. Genel kurallar ve köprü notları kök `CLAUDE.md`'de.

## Klasör ve assembly'ler

- `Scripts/` → `Pota.Runtime` (`Project.Runtime`'a bağlı; ad alanları `Project.Basket`, `Project.Basket.Visual`, `Project.ColorHunt`, `Project.Flow`)
- `Editor/` → `Pota.Editor` (sahne kurma, tarama, kayıt, doku, karakter araçları; ad alanı `Project.Editor.AITools`)
- `Tests/EditMode` → `Pota.Tests.EditMode`, `Tests/PlayMode` → `Pota.Tests.PlayMode`
- `Scenes/` (Basket, ColorHunt, PotaFlow: üçü de Build Settings'te ve `PotaProfile`'da), `Settings/` (ayar asset'leri + fizik malzemeleri), `Art/` (Pota'dan kopyalanan görseller, `Art/Generated` üretilmiş basketbol dokusu, `Art/Fonts`, `Art/Mesh`, `Art/Anim`), `Prefabs/` (PotaHeroToon = oyuncu, eski PlayerCharacter), `Art/Characters/` (Blender teslimleri), `Anim/` (AnimatorController), `Shaders/` (`Project/TextDepth`)
- Ortak juice bileşenleri (`CameraShake`, `TimeScaleController`, `PopupText`, `SynthSfx`) artık Core'da, ad alanı `Project.Juice`. VFX: `Project.VFX.VfxService` (Core). Kod içinden tip adı: `"Project.Basket.BasketRound, Pota.Runtime"`.
- Oyun profili: `PotaProfile.asset` (Game view 1170×2532 = yeni iPhone oranı; ürün adı "Pota Unity", bundle id `com.tunaaltin.pota.unity`, yani gerçek Pota'dan (`com.tunaaltin.pota`) ayrı bir uygulama; Apple takımı `BJPL5XNDLV` (Pota'nın Xcode projesinden); sahneler Basket, ColorHunt, PotaFlow).
- Testler: EditMode kurallar (`AimMapper`, `TrajectorySolver`, `LevelRules`, `RoundRules`, `ColorHuntRules`) ve PotaFlow parçaları (`FlowTimeline`, `CameraFlight`, `GiantHoop`, `BallStream`, `BoardReveal`, `CourtBuilder`, `ColorHuntStaging`, `BasketHud`, `SceneBuildSettings`, `FrameRecorder`); PlayMode `BasketRoundPlayTests` (2 atış), `ColorHuntPlayTests` (3 dokunuş), `ColorHuntHudPlayTests` (düğmeler), `ColorHuntStagingPlayTests`, `BasketHooksPlayTests`, `PotaFlowPlayTests` (14 akış testi), `ColorHuntInputPlayTests`. Core'daki `SceneBootTests` üç sahneyi de açıp 3 sn hatasız çalıştırır. Hepsi tek komutla: `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Project.Tests.Fixture ...]`.
- İlk karakter denemesi (25 Eylül, git'te saklı): `Assets/_Project/Experiments/PotaCharacterExperiment/` (menü `Pota/Character/Build and bake animations`).

## Basket turu (Pota "Faz A" yeniden yapımı, 2026-09-19)

Sahne `Scenes/Basket.unity`, kod `Scripts/Basket/` (saf kurallar: `AimMapper`, `TrajectorySolver`, `LevelRules`, `RoundRules`; bileşenler: `BasketRound` durum makinesi, `Ball`/`BallPool`, `Hoop`, `BasketCage`, `HoopColumnBuilder`, `RoundCamera`, `BasketJuice`, `BasketHud`, `PlayerActor`, `Visual/*`), ayarlar `Settings/BasketSettings.asset` (tüm sayılar orada, koda gömme). Tasarım `docs/superpowers/specs/2026-09-18-basket-round-design.md`, plan `docs/superpowers/plans/2026-09-18-basket-round.md`, ölçüm `docs/measurements/2026-09-19-basket-sweep-L1.md`, görüntüler `docs/screenshots/`. (docs/ repo kökünde, eski yolları tarihsel kayıttır.)

- Araçlar: `ai_basket_build_scene` (sahneyi sıfırdan kurar; katmanlar BallFlight/BallDrop/Hoop/Slab, fizik malzemeleri, sanat referansları), `ai_basket_sweep` (Play Mode'da atış taraması, action start/status/cancel), `ai_basket_ball_texture` (basketbol dokusu), `ai_basket_record` (kare kare kayıt). Menü: `AI Tools/Basket/...`.
- Fidelite notları: uçuş 1 sn parabol, 0.09 sn top aralığı, ×2/+1 kuralları, bölüm tablosu, sepet sayımı orijinalle aynı. 3B'ye özel devir: uçan top pota katmanıyla çarpışmaz, z=0'da Drop fazına geçer (ileri hız ×0.1); 0.45 m üstünden gelen top yanal hız ×0.3 ("panoya çarptı"), 1 m üstünden gelen yana itilir ("panoyu aştı"). Geçiş: yukarıdan giren top düzlemin altında R−r/2 içindeyse sayılır. 1. bölüm taraması: min 12 / medyan 19 / maks 28 (orijinal 12 / 21 / 32).
- Denemek için: Basket sahnesini aç, Play, fareyle aşağıdan yukarı sürükle (sola/sağa pota seçer, uzunluk güç); "Tekrar" düğmesi turu sıfırlar. Bölüm değiştirmek: `Round` objesindeki `Level` alanı.
- **Kare kare inceleme (2026-09-23, Tuna: "video kaydına al, frame frame izle"):** `ai_basket_record` (`Editor/BasketRecordTool.cs`) Play Mode'da programatik atışı PNG dizisi olarak kaydeder (`Temp/record/<ad>/`, ana kamera offscreen RenderTexture'a; kare adında fizik zamanı ve sepet sayısı). Araç kendi küçük yüzen Game view'ını açar, editörü Pause'a alır ve her tick `EditorApplication.Step()` ile tam bir oyun karesi ilerletir (`Time.captureDeltaTime` 1/30). Kolaj/mp4 dışarıda ffmpeg ile (`docs/screenshots/basket_round_L1_*.mp4`, `basket_sheet_*.png`). Bulunan ve düzeltilen: teras levhası oyuncu gizlenince kalıyordu → `PlayerActor.AlsoHide`. Sonuç: uçuş, üst geçiş, ×2 çoğalma, +1, sepete dolma ve kamera inişi temiz; 26/25 sepet, 8 üst geçiş.
- Sıradaki adaylar: üst kuyruk ayarı (alt sıra geçişleri / CloneScatter), telefon dokunmatik testi ve build, Faz B (bulmaca) entegrasyonu.

## Renk avı (Pota 2. aşama denemesi, 2026-09-22)

**Görünüm 2026-09-30'da Pota'nınkiyle değişti (aşağıdaki bölüm); bu bölümdeki blok/efekt/HUD tarifleri ilk denemenin.** Sahne `Scenes/ColorHunt.unity`, kod `Scripts/ColorHunt/` (saf kural `ColorHuntRules` = Pota `src/renkAvi.ts` portu, mulberry32 dâhil; `ColorHuntGame` durum makinesi + animasyon kuyruğu; `BlockView`, `BoardBuilder`+`RoundedBoxMesh`, `ColorHuntFx` (tek parçacık sistemi seti, `Emit` ile renk), `BallSalvo`, `ColorHuntHud` (uGUI, üretilmiş sprite'lar), `ColorHuntJuice` (Core juice bileşenleri), `ColorHuntCamera` (en/boya göre sığdırma), `ColorHuntInput` (Input System pointer)). Ayarlar `Settings/ColorHuntSettings.asset` (bölüm 20: 7×7, 5 renk, can 3–14, cephane 158, hedef 22 kırmızı + 16 mavi; efekt/juice sayıları). Tasarım `docs/superpowers/specs/2026-09-22-color-hunt-design.md`, görüntüler `docs/screenshots/colorhunt_*.png`. Testler `Tests/EditMode/ColorHuntRulesTests.cs` (8/8).

- Araçlar: `ai_colorhunt_build_scene` (sahneyi sıfırdan kurar, Game view'ı 1080×1920'ye alır — Core'daki `GameViewPortrait`), `ai_colorhunt_play` (Play Mode: status / tap {cell} / rotate {dir} / auto {moves} / slowmo {scale, seconds} / retry). Menü: `AI Tools/Color Hunt/...`.
- **Revizyon 2026-09-23 (Pota commit 71f8b9e/f55775d):** bloklar renk + basket eşyası + rakam (`ColorHuntSettings.ItemIcons`, `Art/esya-*.png`; eşya yüzün üst yarısında ölçek 0.62, rakam önde ve altta 0.56 — Pota `etiketleriYaz` düzeni), hedef panelinde eşya ikonu. Unity ekstrası: sayılan bloğun eşyası patlayınca HUD'daki hedef kutusuna uçar ve ikonu zıplatır (`ColorHuntFx.FlyItem`, `ColorHuntHud.TargetScreenPoint/PopTarget`). Quad primitive zaten −z'ye (kameraya) bakar; 180° çevirme mirror + cull yapar.
- **Düğme hatası (2026-09-29, PotaFlow sırasında bulundu):** döndürme okları ve "Yeni tahta" düğmesi, sahne diskten açıldığında hiçbir şey yapmıyordu (dinleyiciler sahne aracında Edit Mode'da ekleniyordu, sahneye kaydedilmez). Artık `ColorHuntHud.Bind` çalışırken bağlar; test `ColorHuntHudPlayTests`.
- Denemek için: ColorHunt sahnesini aç, Play, bloğa tıkla (bas-sürükle-bırak seçimi), alttaki oklar döndürür. Pota projesine taşınmadı; Tuna Unity'de bakıp karar verecek (patlama/efekt hissi, tahta boyu, HUD).

## Renk Avı: Pota'nın bulmaca görünümü (2026-09-30)

Renk Avı artık Pota'nın güncel bulmacası gibi görünüyor, sesleniyor ve oynuyor: bloklar, etiketler, patlama katmanları, salvo, düşüş, dönüş, HUD ve sesler. Oyun kuralları değişmedi (normal bloklar; özel toplar, engeller, güçlendiriciler ve smaç sonraki iş). Tasarım `docs/superpowers/specs/2026-09-30-colorhunt-pota-look-design.md`, plan `docs/superpowers/plans/2026-09-30-colorhunt-pota-look.md`. Görüntüler `docs/screenshots/2026-09-30-potalook-{tap,rotate,win}.jpg` (Pota | Unity aynı anlarda) ve `2026-09-30-potalook-side.mp4` (yan yana video).

- **Pota'dan gelenler:** etiket atlası (2048², 11×11 hücre: 1–99 rakam, yıldız, eşyalar, özeller, engeller), yıldız/iz/yumuşak disk/halka dokuları, basketbol dokusu, 256² eşya çıkartmaları, buzlu pano `pano-a.png`, Lilita One yazı tipi (OFL) ve 22 ses (`Audio/PotaLook/`). Hepsi Pota'nın **kopyasından** üretilir: `Tools/potalook/run_export.sh <scratch>`. Betik Pota'yı scratch'e rsync'ler, kopyayı :5199'da vite ile açar, Pota'nın kendi kodunu sayfada çalıştırıp PNG ve WAV yazar. Sentez sesleri (`kombo`, `carpma`) ve örnekler Pota'nın zinciriyle (oda gönderimi 0,09, ana ses 0,9) WAV'a işlenir. Sonra `execute_custom_tool {"tool_name":"ai_potalook_import"}` çalıştır: içe aktarma ayarlarını, `PotaRoom` katmanını (12), oda yansımasını, ayar bağlantılarını ve `Settings/PotaPuzzleProfile.asset` son işlem profilini kurar. Pota klasörüne hiçbir şey yazılmaz.
- **Sayılar nerede:**
  - `Scripts/ColorHunt/PotaLook.cs` görünümün geometrisini tutar: DEV_POTA ölçüleri, ızgara, kamera sığdırma, CSS düzeni, blok renkleri, ışık, eğriler. Her sabitin yanında Pota'daki kaynağı yazar. Bunları değiştirmek birebirliği bozar.
  - `Settings/ColorHuntSettings.asset` his değerlerini tutar: süreler, şişme zirveleri, sarsıntı, anlık yavaşlama, parçacık sayıları, ışık gücü. Varsayılanlar Pota'nın değerleri; Inspector'dan ayarlanır.
  - three.js ışık yoğunlukları URP'de ÷π (`PotaLook.ThreeToUnityLight`).
- **Tonemapping:** Pota'nın `NeutralToneMapping`'i Khronos PBR Neutral; URP'nin Neutral'ı farklı bir eğri. `ai_potalook_import` projeyi Khronos'a çevirir (`KhronosTonemap`, CLAUDE.md §12). Son inceleme bunu yakaladı: önceki kayıtlarda parlak tonlar %26 sönüktü.
- **Derlemede shader:** Renk Avı ve Basket'in çalışırken yaptığı materyaller `Materials/Templates/*.mat` şablonlarından kopyalanır (`PotaMaterialTemplates`, `ColorHuntSettings`/`BasketSettings` alanları); şablonsuz `Shader.Find` cihazda null döner. `RuntimeShaderBuildTests` denetler.
- **Oda yansıması:** `Editor/PotaRoomBaker.cs` three'nin `RoomEnvironment`'ını (kutu, 6 kutu, 6 ışık paneli, nokta ışık) editörde (0, −400, 0)'da kurar. Odayı küçük bir ışıksız shader (`Shaders/PotaRoom.shader`) ile three'nin formülüyle aydınlatır. Unity'nin probe bake'iyle HDR küp dokuya pişirir: `Art/PotaLook/PotaRoom.exr`, en parlak değer 100. Tahtadaki Custom reflection probe bunu kullanır (şiddet 0,45). Gerçek zamanlı probe'lar iki kalite seviyesinde de kapalı. Ortam ışığı bu pişmiş odanın yön başına ışınımından gelir (yukarı 1,577, ufuk 0,867, aşağı 0,692, × 0,45).
- **Kamera:** 38°. Çerçevenin dış genişliği (9,5) ekran genişliğinin %98,5'i. HUD'un altı ile okların üstü 390 px'lik telefonun CSS pikselleriyle hesaplanır (`PotaLook.CameraFit`, `ColorHuntCamera`).
- **Sahne:** Pota'nın çatısı (`PuzzleStage`):
  - 15×14 saha potadan 3 m önce biter;
  - bina 17×16 #cbbfae potadan 2 m önce biter, üç kenarında korkuluk;
  - dev pota: buzlu cam pano, turuncu çubuklar, çember R 1,6, altı halkalı file (her halka %10 dar), çerçevenin altından zemine direk.

  PotaFlow kendi uzun sahasını korur (`BuildRoof` kapalı).
- **HUD** (`ColorHuntHud`): Pota'nın lacivert gradyan kutuları, altın kenar ve Lilita One rakamları; oklar sağda üst üste. İpucu Pota'nın krem kartında. Eşya patlayınca HUD'daki hedefine 0,55 sn'de uçar, hedef sayısı her varışta birer düşer.
- **Animasyon sırası** (`ColorHuntGame`, `SwellSet`, `PotaFx`, `BallSalvo`):
  1. Dokunulan blok 0,11 sn'de 1,18'e şişer.
  2. **Zirvede** (0,0605 sn) sarsıntı, anlık yavaşlama ve ses gelir. Patlama katmanları: beyaz ve renkli flaş, halka, yassı parlak parçalar, yıldız kıvılcımlar, puf, nokta ışık.
  3. Salvo 0,5 sn: bedel kadar top (en çok 24) kavisle bloğa uçar.
  4. Zincir adımları 0,15 sn arayla; derinlik büyüdükçe sarsıntı ve `kombo` sesi yükselir.
  5. Düşüş: sabit ivme, hücre başına 0,25 sn, inişte ezilme.
  6. Dönüş 0,42 sn. Tahta çerçeveye sığmak için küçülür; etiketler önce tahtayla döner, sonra dikelir.
  7. Kazanınca bloklar dağılır ve kâğıt konfeti yağar.
- **Kaydet ve karşılaştır:**
  1. Unity kaydı: ColorHunt sahnesi Play'deyken `execute_custom_tool {"tool_name":"ai_colorhunt_record","parameters":{"name":"potalook-play"}}` çalıştır. Kazanma kaydı için `"win":true` ekle. Ayrıca `script` (`wait 0.6; select auto; tap auto; settle; rotate cw; …`), `seed` (7), `width` (540) ve `tail` verilebilir. Araç editörü duraklatıp kare kare ilerletir ve `Temp/record/<ad>/f_NNNN_t<sn>.png` + `events.json` yazar. Dosya adındaki süre gerçek oyun zamanıdır (duraklatılmış editör adım başına 0,02 sn ilerler).
  2. Pota kaydı: kopyada vite açıkken `node Tools/potalook/record-pota.mjs http://localhost:5199/ <klasör> play|win` çalıştır. Pencere 390×693, 9:16. Her olayda köşede 10 px'lik macenta işaret yanar; Playwright videosu ~0,35 sn geç başladığı için anlar bu işaretten okunur.
  3. Levhalar: `uv run --with pillow Tools/potalook/compare.py <çıktı-ön-eki> <pota.webm> <pota events.json> Temp/record/<ad> Temp/record/<ad>/events.json`.
- **Bilinen farklar:**
  - Kayıtta HUD da son işlemden geçiyor (`FrameRecorder`); oyunda geçmiyor.
  - Konfeti kazanmada; Pota'da smaçta (smaç sonraki iş).
  - Tahtalar ve hedefler farklı: Pota bölüm 9 (üç hedef), Unity bölüm 20 (iki hedef). Pota'nın ○○○ smaç göstergesi yok.
  - Pota'nın şehir arka planı yok.
  - Karşılaştırma Pota'nın 2026-09-30 03:58 haline göre yapıldı. Pota'da 04:29'daki tasarım turu (alt bar: solda güçlendirmeler, sağda oklar; ipucu tahtanın üstünde) henüz taşınmadı.

## Renk Avı: stüdyo görünümleri A/B ve cila (2026-09-30)

**Karar (2026-09-30 akşam):** Tuna A ile B'yi beğenmedi; Pota görünümü kalıyor ve adım adım iyileştirilecek. İlk adım: Pota görünümü **stüdyo uçuşunu** kullanıyor (kırılan eşya zıplayıp kavisle hedefe uçar, varışta esneme, yıldız, sayaç yuvarlanması, ✓ damgası, `topla`/`tamam` sesleri). Anahtar `ColorHuntSettings.StudioFlight` (varsayılan açık; kapalıyken Pota'nın düz uçuşu). Pota görünümünde diğer cila parçaları (basma, cephane, açılış, ipucu) kapalı. Karşılaştırma videosu `docs/screenshots/2026-09-30-potaflight-side.mp4` (Pota | Unity eski uçuş | Unity yeni uçuş). A/B kodda `Look` arkasında duruyor.

Pota görünümünün üstüne iki "daha iyi" aday ve ortak bir cila katmanı. Tuna A ile B arasında seçecek; seçilen görünüm ayrı bir cila turunda varsayılan olacak. Tasarım `docs/superpowers/specs/2026-09-30-colorhunt-studio-polish-design.md`, plan `docs/superpowers/plans/2026-09-30-colorhunt-studio-polish.md`. Görüntüler: `docs/screenshots/2026-09-30-studio-{tap,chain,win}.jpg` (Pota | A | B aynı anlarda), `2026-09-30-studio-side.mp4` (üç sütun yan yana), `2026-09-30-studio-closeup-{candy,shatter}.mp4` (×2,4 yakın, ×0,3 yavaş).

- **Görünüm seçimi:** `Settings/ColorHuntSettings.asset` → `Look` (Pota / Candy = A / Shatter = B); çalışırken `ColorHuntGame.SetLook(...)`. Görünümün varlıkları eksikse konsola uyarı yazar ve Pota'ya döner.
- **A "Şeker patlaması"** (`Bursts/CandyBurst.cs`, `CandyHud.cs`): blok, bloğun rengine boyanmış mürekkep hatlı bir çizgi film pufuna döner (Blender flipbook), sekiz ışınlı beyaz flaş (her flaş rastgele döner, yan yana patlayanlar ızgara çizmesin), 6–10 dış hatlı şeker parçası kavisle saçılır, yıldızcıklar yanıp söner. Zincirde halka dalgası, yazılar (İyi!, Süper!, Harika!, Muhteşem!; lacivert dış hat), ekran kenarında zincirin renginde parlama, derinlikle artan kamera vuruşu; 5+ zincirde konfeti ve 0,25 sn ×0,5 yavaş çekim.
- **B "Gerçek kırılma"** (`Bursts/ShatterBurst.cs`, `ShatterDebris.cs`): blok Blender'da kırılmış 10 parçaya ayrılır; parçalar blok renginde, ışıklı ve yansımalı, ön yüzlerinde çıkartmanın (eşya + rakam) parçaları kalır. Dışa ve kameraya doğru uçar, döner, çerçevenin iç kenarlarından ve camdan seker, 0,8–1,2 sn'de küçülerek kaybolur. Işıklı duman, çizgi kıvılcımlar, daha güçlü nokta ışık, arkasını büken şok halkası (`Shaders/PotaShockRing.shader`; kameranın opaque dokusu yalnız bir halka yaşarken açık, halka etiketlerin ve efektlerin altında çizilir). Parçaları patlamanın nokta ışığı da aydınlatır. 4+ zincirde 0,3 sn ×0,4 yavaş çekim ve kısa kamera itişi.
- **Ortak cila** (Pota görünümünde kapalı): eşya çıkartması bloktan zıplayıp akan bir kuyrukla HUD'daki hedefe uçar (yay ekrandan taşacaksa ters yöne döner), ikon esner, yıldız patlar, sayaç yuvasında yuvarlanır, bitince ✓ damgası ve yeşil parlama (`Studio/CollectFlight.cs`); bloğa basınca çöker, parlar, bırakınca yaylanır; bedeli cephanenin yanında lacivert bir çipte "−12" (yetmiyorsa kırmızı); salvoda cephane rakamı yuvasında yuvarlanır, en ucuz bloğun iki katından az kalınca kırmızı nabız (`Studio/AmmoCounter.cs`, `RollWindow.cs`); yeni tahta alttan yukarı sıra sıra açılır; 5 sn dokunulmazsa en iyi hamle kıpırdar ve parlar.
- **Sayılar nerede:** cila eğrileri ve süreleri `Studio/StudioCurves.cs`; görünüme özel boylar, renkler ve sayılar `CandyBurst`/`ShatterBurst`; parça hareketi `ShatterDebris` (yerçekimi 12, sekme 0,45, çerçeve ±4,4, cam z 0,7).
- **Varlıklar:** Blender atölyesinde `vfx_flipbook` (tarifler `puff_toon`, `ring_toon`, `smoke_lit`, `shock_ring`) ve `vfx_fracture` (`vfx-pillow-shatter`); Unity'ye `execute_custom_tool {"tool_name":"ai_studio_import"}` getirir ve ayarlara bağlar. Çalışma anı materyalleri `Materials/Templates/` şablonlarından (`PotaMaterialTemplates`: PotaFlipbook, CandyShard, PotaPiece, PotaShockRing dahil 10 şablon).
- **Kayıt:** `Tools/potalook/record_unity.sh AD '{"look":"candy","closeup":true,"script":"wait 0.3; tap 43; wait 5"}'` (ColorHunt'ı açar, Play'e girer, `ai_colorhunt_record` ile kaydeder; `look`: pota | candy/a | shatter/b; `closeup`: ilk dokunulan bloğa ×2,4 yakın ve ×0,3 yavaş). Stüdyo görünümlerinde kayıt tahta açılışıyla başlar, dokunuşlar açılışın bitmesini bekler. Levhalar: `compare.py ÖNEK pota.webm pota-events.json "A (Candy)=Temp/record/a" "B (Shatter)=Temp/record/b"` (anlar: tap, chain, rotate, win). Video: `side_video.py ÇIKTI.mp4 --pota pota.webm events.json --unity "A (Candy)=…" "B (Shatter)=…"` (Unity kareleri oyun zamanına göre 30 fps'e örneklenir).
- **Bu işin davranışı değiştiren kararları:** duman Mantaflow benzetimi değil, gürültülü bir hacim (aynı kimlikle sonra değiştirilebilir); cephane nabzı "hiçbir bloğa yetmeyince" değil en ucuz bloğun iki katının altında başlar (o an tur zaten biter); parçalar PhysX değil, elle yazılmış belirlenimci bir hareketle uçar (kayıtlar aynı tekrar eder); kırık model Unity'ye önü +z'de gelir, parçalar y'de 180° çevrilerek takılır (`ShatterBurst.Mount`).
- **Bilinen farklar / açık noktalar:** ipucu en uzun zinciri gösteriyor, hedef rengini gözetmiyor (U40, Tuna'ya soru); B'de kenar bloğunun parçaları köşede birikip ~1 sn yeni bloğun önünde duruyor (U41); flipbook'lar mobil sıkıştırmasız (U39); karşılaştırmadaki Pota kaydı 2026-09-30 03:58 hali.

## PotaFlow: sepetten tahtaya geçiş (2026-09-29/30)

Tek sahne `Scenes/PotaFlow.unity`: Basket kulesi orijinde, terasın arkasında turuncu çatı sahası, sahanın sonunda dev pota ve içinde Renk Avı tahtası (yalnız ötelenmiş, `BoardAnchor` (0; 8,5; 30)). Basket turu bitince kamera kesintisiz uçar, sepetteki toplar dev potanın filesine akar ve cephane olur. Tasarım `docs/superpowers/specs/2026-09-29-pota-flow-transition-design.md`, plan `docs/superpowers/plans/2026-09-29-pota-flow-transition.md`, görüntüler `docs/screenshots/2026-09-29-potaflow-*` (A ve B kare levhaları, Pota | A | B yan yana levha ve video).

- **Denemek için:** PotaFlow sahnesini aç, Play, aşağıdan yukarı sürükleyerek at (bölüm 20). Tur bitince geçiş başlar; geçiş sırasında ekrana dokunmak kalan kısmı ≤ 0,35 sn'de bitirir. Tahta bitince sonuç kartındaki "Tekrar at" kamerayı nişan pozuna geri uçurur ve Basket turu yeniden başlar.
- **Aday A seçildi (Tuna, 2026-09-30), varsayılan** (`Settings/PotaFlowSettings.asset` → `Candidate`). **A "Akan toplar":** 14 görünür top kafesten fışkırır, kahramanın yanındaki **top sepetine** (`BallBin`, kuledeki kafesin tel örgü tarzı, `BinPosition` (1,4; 0; 7,5)) eğik kavisle düşer, en çok 12'si sepette yığılır; cephane paneli her inişte payını ekler; kahraman sepete döner. Dev potanın filesi boş kalır (dekor). Tuna'nın gerekçesi: "adamın yanına bir sepet koysak, toplar oraya dolsa mantıklı olur". **B "Sade"** ayarda kalır (top akışı yok, rozet kamerayla gider).
- **Zaman çizelgesi** (gerçek zaman, `FlowTiming`): 0–0,3 kafeste "N TOP" + kafes ağzında parlama + FOV vuruşu; 0,25–0,77 toplar çıkar (0,04 sn arayla); 0,6'da kahraman, teras ve sepet görünür; kamera iki vuruşta gider (0,3–1,1 kuleden yükselip yan manzaraya (8,5; 4; 4,5): kahraman + sepet, 1,35'e kadar bekler, 1,35–1,95 bakışı önden giderek tahtaya); 0,9–1,25 kahraman sepete döner; 0,9'da cephane paneli girer, 0,95–1,45 toplar sepete iner ve sayaç artar; 1,75'ten tahta ortadan dışa halka halka zıplar, çerçeve parlar; 1,9–2,25 hedef paneli ve oklar girer; 2,5 hazır.
- **Spec'ten ayar farkları (kayıtlardan sonra, gerekçeleriyle):** toplar fileye değil kahramanın sepetine (Tuna); inişler kamera kahramandayken (0,95–1,45); 25 yerine 14 daha küçük top (r 0,19; 25 top kameraya yakın tek bir "tırtıl" zincir gibi görünüyordu); cephane paneli 1,90 yerine 0,9'da (sayım görünmüyordu); manzara (13; 7; 17) yerine (8,5; 4; 4,5), bakış kahraman ve sepete (sepet ve kahraman birbirini kapatmıyor, Basket görüntüsünde sepet kahramanın yanında); ikinci kamera vuruşunda bakış önden gider (`LookLead`, boş gökyüzü karesi kalmadı); ipucu yazısı PotaFlow'da gizli (dev potanın filesinin üstüne biniyordu). Görüntüler `docs/screenshots/2026-09-30-potaflow-basket-{sheet.jpg,side.mp4}` (Pota | eski A | yeni A).
- **Kod** (`Scripts/Flow/`, ad alanı `Project.Flow`): `FlowTimeline` (saf; her t için top çıkış/iniş, paylar toplamı tam N, kamera ilerlemesi, halka ölçeği, HUD, atlama hızı), `CameraFlight` (saf Catmull-Rom bakış+göz yolu), `GiantHoop` (cam pano, turuncu çerçeve, çember R 1,6, 6 halkalı file, direk), `BallBin` (kahramanın top sepeti), `BallStream` (havuzlu görsel toplar, `IBallReceiver`'a deterministik yol, her iniş tek olay), `BoardReveal`, `CourtBuilder` (Pota `saha.ts` #d9773a zemin + beyaz çizgiler, kodla çizilen doku), `FlowBadge`, `PotaFlowSettings`, `PotaFlowDirector` (Basket → Geçiş → Bulmaca → Sonuç → Dönüş; faz başına girdi, kamera bileşeni, sis 26–115 → 60–220, HUD). Mevcut koda küçük kancalar: `ColorHuntGame.Prepare/ShowAmmo/Begin/AutoStart`, `ColorHuntCamera.Anchor/FitFor`, `ColorHuntHud.SetIntro/RetryOverride/ShowHint`, `BasketHud.ShowResultPanel`, `RoundCamera` (etkinleşince bulunduğu yerden devam, `LookPoint`), `PlayerActor.Reveal`, Core `TimeScaleController.Clear`.
- **Araçlar:** `ai_potaflow_build_scene` (sahneyi Basket ve Renk Avı yardımcılarıyla kurar, Build Settings'e ve `PotaProfile`'a ekler), `ai_potaflow_record` (`{"candidate":"A"|"B","count":158,"shoot":true}`: bölüm 20'yi programatik atışla oynatır, geçişi 30 fps PNG olarak `Temp/record/<ad>/` altına yazar; UI dâhil), `Tools/frame_sheet.py` (kare klasörlerinden zamanlı levha). Kayıtta overlay UI kameraya düşmez: `FrameRecorder` overlay canvas'ları kayıt süresince kamera kipine alır; rozet bu yüzden ekran oranı çapasıyla yerleşir.
- **Pota'dan alınan sayılar:** dev pota ölçüleri `src/sahaYerlesim.ts` DEV_POTA ve `src/pota.ts` (çember R 1,6, panonun altında ve öne R+0,6, 6 halkalı file), zemin rengi `src/saha.ts`, akış `src/akis.ts` (`fazABitti` → `fazBBasla`, cephane = sepet sayısı), kamera `src/kameraYonetmeni.ts` (1,4 sn karışım; bizimki ~2,5 sn, dokununca kısalır). Pota karşılaştırması Pota'nın **kopyasından** kaydedildi (scratch'te rsync + vite + kendi Playwright betiğimiz); Pota klasörüne hiçbir şey yazılmadı.

### PotaFlow: üç yeni aday (2026-10-02; Tuna **3 "Pano damgası"**nı seçti, varsayılan artık `Stamp`)

Tuna "basketten bulmacaya geçiş için 3 alternatif, aynı varlıklarla" istedi. Tasarım `docs/superpowers/specs/2026-10-02-potaflow-candidates-design.md`,
plan `docs/superpowers/plans/2026-10-02-potaflow-candidates.md`. Üçü de A'nın ilk 1,45 sn'sini paylaşır (kafes vurgusu, toplar kahramanın
sepetine, kahraman sepete döner, kamera yan manzaraya); fark sepet dolunca başlar. Seçim `PotaFlowSettings.Candidate`; kayıt
`ai_potaflow_record {"candidate":"1"|"2"|"3"}`. Teslim: `docs/screenshots/2026-10-02-potaflow-candidates-{sheet.jpg,side.mp4}`
(Pota | A | 1 | 2 | 3; Pota güncel main'den, scratch kopyasıyla), U26 için `…-u26-court-vs-roof.jpg`.

- **1 "Büyük şut"** (`BigShot`, hazır 3,30): kahraman dev potaya döner ve `Shoot` klibini oynatır (1,55); sepetteki 12 top 1,65'ten
  0,03 sn arayla fışkırır (`NetStream`, kaynak sepet yuvaları, hedef `GiantHoop`), 2,45–2,85 çemberden fileye düşer, file şişer ve
  sallanır; **cephane yalnız file inişleriyle sayılır** (sepete inişler saymaz), rozet ilk inişle panele uçar. Kamera: manzara → atış
  pozu (`ShotPos` (−1,4; 5; 0,9), bakış (0; 1; 30), FOV 66: kahraman altta arkadan, dev pota üstte; kule ön plakası z 0,7'de biter)
  1,50–2,05, bekler, 2,55–3,05 tahtaya. Tahta 2,60'tan halka halka, HUD 2,85.
- **2 "Çemberden dalış"** (`Dive`, hazır 3,10): toplar sepette kalır, sayım A gibi. Kamera 1,50'den sahanın üstünden hızlanır
  (`DiveCourtPos` (0; 1,8; 20)), filenin altından (çember −1,9) çember ekseninde (çemberin 0,3 önünde, kol direğinden uzak) yukarı
  çıkar — 2,20'de çember düzlemi (`CameraFlight.PathUAtHeight`), `swish` + file sallanır + Court'un halka dokusuyla swish halkası +
  "SWISH!" yazısı (`vfx-letter-swish`, 3,6 m) — çemberin 1,7 üstünden tahta pozuna geri çekilir (2,90). Kenarlarda hız çizgileri
  (`vfx-speed-lines`, kamera önünde iki aynalı şerit, 1,60–2,25). Bloklar 2,25'ten (kamera uzaklaşırken yüze yakın "pat pat"), HUD 2,55.
  Yol Hermite: yükseliş iki ucunda dikey teğet, yoksa Catmull-Rom 2 m yana şişip filenin içinden geçiyordu.
- **3 "Pano damgası"** (`Stamp`, hazır 2,90): tek kayma 1,45–2,15 (Pota `pozKaris`: smoothstep + ortada 3,5 m yükselme), rozet kamerayla
  gider ve 2,15'te cam merkezine damgalanır (ölçek 1 → 2,2 → 1,8; `vfx-puff-toon` 6 puf + yıldız flaşı, parçacıklar yönetmenin
  saatiyle `Fx.Advance` ile ilerler — duraklatılmış kayıtta kendi zamanları tutmuyordu), çerçeve parlar, kamera itiş-oturuş; 2,20'den
  bloklar sütun sütun (0,04 sn) çerçevenin 8 hücre üstünden sabit ivmeyle yağar (`BoardReveal.ApplyRain`, inişte Pota'nın ezilmesi);
  cephane paneli 2,40'ta girer, rozet 2,45–2,70 panele uçar.
- **Kod:** `FlowTimeline` (`BigShotTiming`/`DiveTiming`/`StampTiming` blokları, `IBallSchedule` + `BallSchedule`, aday başına `Ready`,
  `CameraEndTime`, `RevealStartTime`, `CountAt`, `BadgeFly`, dalış/damga eğrileri), `CameraFlight.Path` (N nokta, Hermite, teğet
  geçersiz kılma), `.Sweep`, `.PathUAtHeight`; `BallStream.Begin(schedule, sourceOf, target, holdFrom)` (top uçana kadar kaynağında
  bekler); `PlayerActor.PlayShoot`; `FlowEffects` (hız çizgileri, swish halkası/yazısı, damga pufu; `ColorHuntSettings` dokuları,
  şablon materyaller); `PotaFlowDirector.CameraPose/ApplyEffects`. Ayarlar `PotaFlowSettings` ("Candidate 1/2/3" başlıkları).
  **Not:** sınıfa sonradan eklenen bir alanın varsayılanı bellekteki asset'e ilk derlemede yazılır, sonraki varsayılan değişiklikleri
  oraya gitmez; değeri `SerializedObject` ile asset'e yaz (`ShotPos` böyle düzeltildi, asset kaydedildi).
- **Kayıt:** `Tools/potaflow_record.sh <A|B|1|2|3|wait> <ad>` (başlatır, her 8 sn Unity'ye odak verir — odak yokken oyun döngüsü
  durur ve kayıt dakikalarca sürünür, bitince sonucu basar); araç artık tahtada kalmış bir önceki kayıttan `ReturnToBasket` ile
  kendi döner. Yan yana video `Tools/potaflow_side.py OUT.mp4 Etiket=klasör …` (sütunlar kendi zamanına göre yeniden örneklenir,
  kısa sütun son karesini tutar). Pota sütunu: scratch kopyası + vite :5199 + `pota-record.mjs` (`__pota.sepetBitir(158)`, mor
  işaretle hizalanır).
- **Pota'ya teslim (2026-10-03):** `docs/handoff/2026-10-03-pota-gecis-damga.md` — 3 Damga'yı Pota'da (three.js) kurmak için zaman çizelgesi, formüller, varlıklar, sesler, referans kod. Pota oturumu bunu okur; buradan Pota'ya yazılmaz.
- **Top boyu (Tuna, 2026-10-02: "toplar neden sepete kocaman doluyor?"):** geçişin topları 0,19 yarıçapla çiziliyordu; bu sayı
  Pota'nın tahta ölçeğindeki salvo topundan kopyalanmıştı, oysa turun kendi topu 0,12 (`BasketSettings.BallRadius`). Düzeltildi:
  `PotaFlowSettings.BallRadius` 0,12, sepet bel hizasında 0,92 × 0,72 × 0,92 ve 3 × 3 × 3 = 27 top alıyor (`BallBin.Layer`),
  görünür top 27 (0,022 sn arayla), aday 1'de 27'si de fileye uçar (0,02 sn arayla; filede 10 durur). Önce/sonra: `docs/screenshots/2026-10-02-potaflow-ball-size-sheet.jpg`; levha ve video yeni boyutla yenilendi.
- **Testler:** `FlowTimelineTests` (+8), `CameraFlightTests` (+4), `PotaFlowPlayTests` (+3: üç aday oynanabilir tahtada biter,
  atlama, damga yağmuru). Dürüst notlar: Blender'da yeni varlık üretilmedi (mevcut flipbook'lar kullanıldı); adaylar A'dan 0,4–0,8 sn
  uzun (dokunma atlaması ≤ 0,35 sn); Pota bu arada sapan atışı ve parmakla tahta çevirmeye geçti, kendi geçişi hâlâ 1,4 sn tek kayma.

## Oyuncu karakteri (2026-09-25)

Karakter hattının kendisi atölyenin parçası (`Tools/character/README.md`, özet `docs/2026-09-25-character-pipeline.md`). Pota'daki sonuç: `Prefabs/PlayerCharacter.prefab` (rig `Art/Mesh/player_rig_a.glb` + `.avatar.asset`, controller `Anim/PlayerCharacter.controller`), klipler `Art/Anim/player_*.fbx`. `BasketSettings.PlayerPrefab` buna bağlı, `PlayerActor` Shoot'u `PlayerShootClipStart`'tan oynatır. Koşu/smaç klipleri controller'da var, oyun tetiklemiyor.

### Oyuncu: toon kahraman (2026-09-29, Tuna onayladı)

**Oyundaki oyuncu artık `Prefabs/PotaHeroToon.prefab`** (`BasketSettings.PlayerPrefab`). Parçaları:
- toon materyal `PotaHeroToon_Toon.mat`
- controller `Anim/PotaHeroToon.controller`
- rig `Art/Characters/pota-hero/pota-hero_rig.glb` + `.avatar.asset`

Eski `PlayerCharacter.prefab` yerinde duruyor, silinmedi.

Nasıl yapıldı:
1. Düz renkli (albedo) ön, yan ve arka tasarım görünümleri.
2. Rodin Gen-2 "high" kalitede model.
3. Blender `character_model`: 8480 üçgen, kapalı mesh, tasarım görünümünden 10 renklik doku, 2× süpersample. Teslim `Art/Characters/pota-hero/`.
4. Uthana otomatik iskelet.
5. `ai_character_setup`, ardından `ai_toon_material` (Core `Project/Toon`: iki tonlu gölge, kenar parlaması, dış hat).

Aynı `player_*` kliplerini oynar; kemik yükseklikleri eski oyuncuyla ±2 cm.

Görüntüler `docs/screenshots/`:
- `2026-09-29-pota-hero-v2-shot-{a,b}.jpg`: oyunda atış.
- `…-v2-{front,back}.jpg`

Model raporu: `~/projects/Blender MCP/docs/reports/character-model-comparison.md`.

- `ai_basket_build_scene` artık atanmış oyuncu prefabını korur; boşsa `PotaHeroToon`'u koyar (eskiden her kurulumda statik `oyuncu.glb`'ye dönüyordu). Test: `Tests/EditMode/BasketSceneToolTests.cs`.
- `ai_character_setup` (`Editor/CharacterSetupTool.cs`) varsayılanları Pota'ya göre (klip adları `player_*`, `BasketSettings.PlayerPrefab` ataması), o yüzden burada duruyor. İkinci bir oyun karakter isterse varsayılanları parametreye çevirip Core'a taşı.

## Telefonda deneme (iPhone)

1. Claude derler: `ai_build {"action":"start","game":"Pota"}` → `Builds/Pota-ios/Unity-iPhone.xcodeproj` (ilk derleme iPhone moduna geçişle birkaç dakika, sonrakiler saniyeler; 2026-09-28'de Unity derlemesi ve imzasız `xcodebuild` ikisi de başarılı). Tek sahne için `"scene":"ColorHunt"`.
2. Tuna kurar: `open Builds/Pota-ios/Unity-iPhone.xcodeproj` → iPhone'u kabloyla bağla → Xcode'un üst çubuğunda hedef olarak telefonu seç → ▶ (Run). İmza otomatik, takım Pota'nınki. İlk kurulumda telefonda Ayarlar > Genel > VPN ve Cihaz Yönetimi'nden geliştiriciye güvenmek gerekebilir.
3. Uygulama "Pota Unity" adıyla, gerçek Pota'nın yanına ayrı uygulama olarak kurulur ve Basket'le açılır. Üstteki "→ ColorHunt" düğmesi (yalnız test derlemesinde) sahneler arasında geçer.

## Renk Avı: özel toplar ve efekt adayları (2026-10-01)

Tuna'nın isteği: Pota'nın özel toplarına (çizgi, bomba, altın) Royal Match tarzı efektler, Blender + Unity ile iki aday, Tuna seçer.
Tasarım `docs/superpowers/specs/2026-10-01-colorhunt-special-balls-fx-design.md`, plan `docs/superpowers/plans/2026-10-01-colorhunt-special-balls-fx.md`.

- **Kurallar:** Pota `src/renkAvi.ts`'in özel top ve geçiş kısmı birebir (`ColorHuntRules.Specials.cs`). Doğrulama Pota'nın kendi koduyla
  üretilen fikstürlerle (`Tools/potalook/run_fixtures.sh <scratch>` → `Tests/EditMode/Fixtures/special-rules.json`,
  `SpecialRulesFixtureTests`). Bölüm 20'de kapalı (`ColorHuntSettings.Specials`); engeller, güçlendiriciler, bonus, özel hedefler U25'te.
- **Görünüm seçimi:** `ColorHuntSettings.SpecialLook` = Pota / Royal (1) / Court (2); kod `Scripts/ColorHunt/Specials/`
  (`ISpecialStyle`, `SpecialSchedule`, `GoldenPath`, `PotaSpecials`, `RoyalSpecials`, `CourtSpecials`, `GateRings`).
- **Vitrin:** `Scenes/Specials.unity` (`ai_specials_build_scene`): senaryo düğmeleri (Doğuş, Çizgi, Bomba, Zincir, Altın, Serbest,
  Bekleme, Pas, Pas→B), görünüm düğmeleri (Pota | 1 | 2), Tekrar, bekleme düğmeleri (Görünüm · A–E, 2026-10-07), pas satırı (A Pas · B
  Pervane, 2026-10-11).
- **Blender varlıkları:** `vfx-fire-toon`, `vfx-flame-jet`, `vfx-dust-ring`, `vfx-speed-lines`, `vfx-floor-crack`, `vfx-letter-*`,
  `vfx-ball-halves`, `vfx-bomb-shell`, `vfx-coin`, `vfx-basketball` → `Art/VFX/<id>/` (`ai_studio_import`).
- **Kayıt:** `ai_colorhunt_record` `{"scene":"Specials","scenario":...,"special_look":...}` (`scenario` birth/line/bomb/chain/golden,
  serbest kaydedilmez; `special_look` pota/royal(1)/court(2); yakın planlar `closeup` + `zoom`: bomba ×1,3, çizgi ×1,5); sesler
  `events.json`'a düşer; `Tools/potalook/seq_video.py` sesli sıralı video, `side_video.py` sessiz üç sütun.
- **Teslim:** `docs/screenshots/2026-10-01-specials-*` (levha, yan yana video, sesli sıralı video, yakın planlar).
- **Ölçüm:** Mac kilitliyken ~7 fps editörde ölçüldü; Royal +0,24 ms, Court +0,36 ms GPU, Pota'ya göre; canlı parça oranı bütçenin
  0,86 (Royal) / 0,54 (Court)'ü. Kilit açıkken `Tools/potalook/record_specials.sh cost` ile yeniden ölçülecek (U45).
- **Bekleyen:** Tuna'nın seçimi (1 ya da 2); seçilen ayrı bir cila turunda varsayılan olur ve bölüm tablosuyla oyuna girer (U25).
  Tuna'nın gözüyle karar bekleyen diğer noktalar: doğuşta/SLAM!'de/COMBO!'da ve Royal'ın ateş topunun üstünde HUD çıkartmalarının
  uçması (en görünürü altın doğuşu: Royal/Court'ta toplanan beş formanın çıkartması tam patlama anında aynı hücreden kalkıp topu
  ~0,5 sn kapatıyor, U48); Court'un file halkasının küçük kalması (0,9 hücre, ekrana sığdırmak için düşürüldü); uzun düşüşlerde
  kare üstünde doldurma bloklarının görünmesi (Pota'da da var, U47); Royal'ın zincirinin üçüncü ateşe dokunuştan 0,64 sn sonra
  ulaşması (kayıt saatinde — kombo yavaş çekimi ve dip'ler yüzünden; oyun-içi zaman sınırı 0,6 sn hâlâ tutuyor, bu bir saat
  eseri, düzeltme gerekmiyor).

### Özel top bekleme alternatifleri (2026-10-07)

Tuna Build 68'deki Royal beklemesine "çok dikkat dağıtıyor" dedi; spec `docs/superpowers/specs/2026-10-07-special-idle-alternatives.md`,
rapor `docs/reports/2026-10-07-idle-alternatives-report.md`. Pota'ya dokunulmadı; seçilen ayrı bir işte taşınır.

- **Anahtar:** `ColorHuntSettings.SpecialIdle` = Görünüm (seçili özel görünümün kendi beklemesi, birebir) · A Breath · B Glint · C Rim ·
  D Twinkle · E Sway; sayılar `ColorHuntSettings.Idle` (`IdleTuning`). Oyunda `ColorHuntGame.SetSpecialIdle`. Kod
  `Specials/SpecialIdleFx.cs` (A/E yalnız `BlockView.IdleScale`/`StickerSpin`; B/C/D topun çıkartmasının önünde tek havuzlu quad,
  `Shaders/PotaGlint.shader`, LateUpdate'te çizilir). Blender stilleri `vfx-glint-streak`, `vfx-rim-ring`, `vfx-twinkle-star`
  (`ai_studio_import`; `StudioImportTool.WireIdleStills`).
- **Vitrin:** Specials sahnesinde üçüncü düğme satırı "Görünüm · A · B · C · D · E" ve senaryo düğmesi **Bekleme** (bomba, iki çizgi,
  altın, ortadaki 24. hücrenin dört yanında; dokunuş yok).
- **Kayıt:** `ai_colorhunt_record` `{"scene":"Specials","scenario":"idle","special_look":"royal","special_idle":"glint","zoom":2.4}`
  (`special_idle` look/breath(a)/glint(b)/rim(c)/twinkle(d)/sway(e), kayıttan sonra eskisine döner; `idle` 6 sn, odak 24; `closeup`
  olmadan verilen `zoom` normal hızda yaklaştırır). Levha `Tools/frame_sheet.py … --transpose` (seçenekler sütunda), video `seq_video.py`.
- **Teslim:** `docs/screenshots/2026-10-07-idle-alternatives-{sheet.jpg,.mp4}`.

### Pas topu: 2×2 kare → pervane, iki görünüm (2026-10-11)

Pota'nın `pas-topu` dalı (spec `~/projects/Pota-pas/docs/superpowers/specs/2026-10-11-pas-topu-design.md`, Task 9): 2×2 aynı renk kare
bir eşleşmedir ve **pas topu** doğurur; dokununca (bedava) ya da zincirde ateşlenince TEK bir hedefe uçar ve onu altın vuruşu gibi kırar.
Unity'de iki koreografi var, Tuna seçecek; seçilenin sayıları Pota'ya satır satır gider.

- **Kurallar** (`ColorHuntRules.Specials.cs`, `ColorHuntRules.cs`): `SpecialKind.Pass = 5`, `Features.Pass` (`Features.Full` = Pota
  `OZELLIK_TAM`; `Features.All` pas'sız kalır = `OZELLIK_HEPSI`), `FindSquares`, koşu + kare grupları (`GroupShapes`), öncelik altın >
  bomba (yalnız koşular kesişirse) > çizgi > pas (en alttaki karenin sol-alt hücresi), `PassTarget` (hedefte olan önce, bütün tahta,
  Chebyshev, indeks; yoksa en yakın normal blok; altın asla), adımın patlamalarından SONRA pas dalgaları (`FlyPasses`: normal blok
  patlar, özel ateşlenir, alanındaki pas bir sonraki dalgada uçar), `Step.Passes` (`PassHit {Cell, Target}`), `Create` pas açıkken kare
  reddeder. Pota'nın bomba/çizgi hedefleri `Board.BombTarget` / `LineTarget` (HUD'da yok; pas onlara uçar, tetiklenen sayılır).
  Engeller (file/buz/hakem) ve Pota'nın yeni altını (doğunca vurur) Unity'de hâlâ yok (U25): pas açık replay'lerde altın kapalı.
- **Altın kayıt:** `Tools/potalook/pass_fixtures.ts` Pota'nın kendi `src/renkAvi.ts`'iyle `Tests/EditMode/Fixtures/pass-rules.json`
  yazar (11 eşleşme tahtası, 7 dokunuş tahtası, 5 dağıtım, pas açık 8 rastgele oyun, iki senaryo); `PassRulesTests` her adımı alan
  alan karşılaştırır (patlayan, tetiklenen sırası, pas vuruşları, doğum, düşüş, dolum, rng, hedef). Pota kuralı değişirse betiği yeniden
  koş (başlığındaki komut), json'u commit'le.
- **Plan** (`SpecialSchedule`, Pota `ozelPlani`): pasın vurduğu özel planın KÖKÜ olur, top vardığı an ateşlenir (sıkışmaz); normal
  hedef şişmez, varışta patlar (`TriggerPlan.Hits`). Varış = `PassTiming.Arrival` (Pota `pasVarisi`). Görünüm aynı saatle uçar:
  `PassFlight.Tick` oyunun işlerinden önce çağrılır, top hedefin patladığı karede iner ("varış = ateş aynı kare", `PassPlayTests`).
- **Görünümler** (`ColorHuntSettings.PassLook`, oyunda `ColorHuntGame.SetPassLook`, kod `Specials/PassFlight.cs`, sayılar
  `PassLookA` / `PassLookB`): **A Pas** — topun kendi bloğu (gök mavisi + `Art/Specials/pas-topu-a.png`, Pota'nın a2 ikonu) kalkışta
  zıplar ve döner, alçak kavisle uçar, hız çizgileri, varışta flaş + sarsıntı + `carpma`. **B Pervane** — blok gizlenir, Blender
  `pass_ball` (`Art/VFX/pas-topu`, Basketball 960 + Propeller 96 üçgen, toon + mürekkep) yumuşakça yükselir, pervane kalkışta 40 rad/sn'ye
  hızlanır, yüksek kavis (yol smoothstep), hedefin üstünde asılı kalıp hızlanarak dalar, toz halkası — Pota'nın `pasIlerletB`'si birebir
  (4052db9). Top kameraya 0,9 rad (51,6°) eğik (`PassLookB.Tilt` = Pota `PAS_EGIM`; eğimsiz pervane kameradan bıçak gibi ince görünüyor).
  Kalkışta `pas` sesi (Court'un "vıııt"u), inişte `carpma`.
- **Vitrin:** Specials sahnesinde senaryo düğmeleri **Pas** (dokunuş → kare → Royal doğuş → 1 sn bekleme → pasa dokunuş → uçuş →
  çarpma) ve **Pas→B** (pas + uzakta bomba, yalnız bomba hedefi: bomba VARIŞTA patlar); yeni satır "Pas: A Pas · B Pervane".
- **Kayıt:** `Tools/potalook/record_unity.sh pas-a-pass '{"scene":"Specials","scenario":"pass","pass_look":"a","special_look":"royal",
  "special_idle":"breath","closeup":true,"zoom":2.0}'` (`passBomb`: `zoom` 1.25); işaretler `tap, born, tap2, fire, hit, fire2, finish`.
  Kareler `Temp/record/pas-{a,b}-{pass,passBomb}`, levha `docs/screenshots/2026-10-11-pas-ab.jpg`.

## Oyun içi his katmanı (2026-10-03)

Tuna'nın isteği: Pota'nın okunmasından çıkan eksik his listesindeki her şeyi (ses, müzik, titreşim hariç) Unity'de yap, Tuna videolardan
bakacak. Tasarım `docs/superpowers/specs/2026-10-03-pota-ingame-feel-design.md`, plan `docs/superpowers/plans/2026-10-03-pota-ingame-feel.md`,
Pota'ya teslim `docs/handoff/2026-10-03-pota-oyun-ici-his.md` (anlar, sayılar, varlıklar, Tuna'nın seçim tablosu).

- **Anahtar:** `ColorHuntSettings.Feel.Enabled` ve `BasketSettings.Feel.Enabled` (varsayılan açık; kapalı = eski davranış birebir, Önce | Sonra
  kayıtları için). Stüdyo cilası (`ColorHuntGame.Polish = Studio || Feel`) Pota görünümünde de çalışır: açılış, ipucu, cephane nabzı/yuvarlanma,
  zincir kelimeleri; dokunuş çökmesi ve bedel çipi A/B'ye özel kaldı.
- **Renk Avı** (`Scripts/ColorHunt/Feel/`): `LevelIntro` (F1 şerit + hedef çipleri + dalga; PotaFlow'da şerit damga anında, çipler panel girince),
  `AmmoTension` (F2), `LeftoverFinale` (F3; `ColorHuntGame.End` dağılmayı finale bitince çağırır), `LoseMoment` (F9; kart `Feel.LoseHold` 1,2 sn
  sonra), `BoardClip` (F7 stencil maskesi: `PotaBoard` katmanı + PC/Mobile renderer'larda iki `RenderObjects` özelliği, `ai_install_board_clip`
  kurar; maske quad'ı Default katmanda ve çerçeve çubuğunun derinliğinde, yoksa kendi testine takılıp bütün tahtayı gizliyor / perspektif şerit
  bırakıyordu), `FeelCurves` (saf eğriler). F4 `PotaBurstStyle.ChainPeak`, F5 `ColorHuntHint.Score` (U40: hedef eşya puanı), F6 `BlockView.Bounce`.
- **Gökyüzü** (`Scripts/Flow/SkyLife.cs`, F8): bulutlar, kuş sürüsü (`flap` klibi PlayableGraph ile), şehir bandı (U27). Sahne başına bir tane
  (`SkyLife.Ensure`); basket ortamı ve bulmaca sahnesi paylaşır. Blender tarifleri `recipes/{cloud_set,bird_flock,city_band}.py`,
  `vfx_flipbook lettering` `miss` stili; `ai_studio_import` dört varlığı `Art/Props/`, `Art/VFX/vfx-letter-miss/`e alır ve iki ayar asset'ine bağlar.
- **Basket** (`Scripts/Basket/Feel/`): `SlingshotFeel` (F10; `ShotInput.Pulled/Released`, `PlayerActor.SetLean/HandBall`, `Hoop.SetGlow`,
  `AimPreview.SetPower`, `RoundCamera.PullOffset/PullPitch`), `MissFeel` (F11; `LetterPop` KAÇTI!, `Hoop.Blink`, `PlayerActor.SetSlump`,
  `RoundCamera.SagOffset`; eski "Kaçtı" yazısı ve popup Feel açıkken kapalı), `SwishFeel` (F12; `Ball.TouchedRim`), `BasketCountFeel` (F13;
  `BasketHud.Roll/PopAmount`, `FlowBadge` damgası yalnız tek başına Basket sahnesinde). Kafes parlaması yapılmadı (kafes duvarları görünmez).
- **Kayıt:** `ai_colorhunt_record` `feel` (on|off, sonra geri alınır) ve ColorHunt sahnesinde `scenario` intro | lowammo | finale | lose | chain |
  idle (script/ammo/win/tail ön ayarları; işaretler `start`, `result`, `finale`); `ai_basket_record` `mode` shot | pull | miss | swish, `pull`
  (rampa sn), `feel`; `events.json` {start, pull, shot, miss, end, meta} yazar, kareler artık oyun saatiyle adlanır (adımlı editörde kare başına
  ~5 ms; 1/30 sayımı 7× hızlı çıkıyordu). Toplu: `Tools/feel_record.sh [record|videos|all] [an …]` → `docs/screenshots/2026-10-03-feel-<an>-{side.mp4,sheet.jpg}`
  ve `…-feel-all.mp4` (Önce | Sonra; Pota sütunu yok: Playwright Chromium'u öne alır, Tuna uygun saatte isterse eklenir).
- **Testler:** EditMode `FeelCurvesTests`, `BoardClipInstallerTests`, `BasketRecordToolTests`, eklemeler (`ColorHuntHintTests`, `BurstStyleTests`,
  `BlockViewTests`, `StudioAssetsTests`, `ColorHuntRecordToolTests`); PlayMode `FeelPlayTests`, `BasketFeelPlayTests`. Blender `tests/test_recipes_feel.py`.
  2026-10-03 sonu (inceleme düzeltmeleri sonrası): EditMode 428/428, PlayMode 156/156 (önceki 387 / 128); Blender `tools/run.sh test` yeni 3 tarif testi + miss levhası.
- **Dürüst notlar:** Pota'ya dokunulmadı; menü/ödül katmanı (kazanma penceresi, harita, ana ekran, market, öğretici eli) 2. alt proje. Finale
  paraları ve topları `Default` katmanda (maske kırpmasın). Kahramanın eğimi arkadan bakınca az okunuyor; başı potaya çevirme yapılmadı.

## Menü ve ödül katmanı (2026-10-03, 2. alt proje)

Pota'nın menü katmanı Unity'de birebir (`Scenes/Meta.unity`, Pota görselleri + Fredoka) ve üstüne M1–M11 his anları. Tasarım
`docs/superpowers/specs/2026-10-03-pota-meta-feel-design.md`, plan `docs/superpowers/plans/2026-10-03-pota-meta-feel.md`, UI rehberi
`docs/reference/2026-10-03-pota-meta-ui-style.md`, Pota'ya teslim `docs/handoff/2026-10-03-pota-menu-odul-his.md` (F belgesiyle birlikte).

- **Anahtar:** `MetaSettings.Feel.Enabled` (`Settings/MetaSettings.asset`; `MetaFeelTuning.T(sn)` kapalıyken 0). Örnek kayıt `MetaSettings.Save`
  (bölüm 20, 145 para, 3 can, 11 800 kupa, skor kalemleri, madalya eşikleri 300/420, cephane 158, hedef). Kurallar saf: `MetaRules` (`Win/Lose/Score/BarFill`).
- **Varlıklar:** `ai_potameta_import` (Pota `public/assets` → `Art/Meta/`, Fredoka 600/700 → `Art/Fonts/`; `kahraman-maske.png` okunabilir+sıkıştırmasız,
  market forma rengi için). `ai_studio_import` artık `vfx-hand-pointer` ve `vfx-cloud-still`i de alır ve `MetaSettings.HandSprite/CloudSprite`'a bağlar.
- **Kit** (`Scripts/Meta/Ui/`): `MetaStyle` (palet, eğriler, sprite üreticileri; `Ring` = boş halka), `MetaButton` (basma/bırakma, `Pulse`), `MetaWindow`
  (örtü/kutu/şerit, `Fit`), `RollingNumber`, `CoinFlight` (`CoinPlan.Count`), `Toast`, `ScreenSlide`, `Confetti`, `MetaUiKit` (satır/etiket/kart).
- **Ekranlar** (`Scripts/Meta/Screens/`): `MenuScreen` (M7), `TabBar`, `LevelWindow`, `WinWindow` + saf `WinSequence` (M4), `LoseWindow` (M5), `MapScreen` +
  saf `MapLayout`/`MapProgress` (M6), `LeagueWindow`/`PromotionWindow` (M8), `BonusWindow` + saf `ReelSpin`/`BonusRules` (M9), `MarketScreen` + `MarketCatalog`
  (M10), `TutorialHand` (M11), `SettingsWindow`, `DevStrip` (vitrin şeridi). Ekranlar `MetaDirector.Awake`'te çalışma anında kurulur (Edit Mode'da kurulan
  kitin özel alanları kaydedilmiyordu); sahne aracı `ai_meta_build_scene` yalnız kamera/EventSystem/canvas/yönetmen koyar.
- **Yönetmen:** `MetaDirector` — Oyna → bölüm penceresi → karartma; şerit Kazan → `WinSequence` → Devam → (terfi penceresi →) harita ilerlemesi (his kapalı: ana
  ekran); Kaybet; Terfi; Bonus; Öğretici; lig rozeti → lig penceresi (SEN kayması). `GainCoins/SpendCoins` para uçuşu + sayaçlar.
- **Kayıt:** `ai_meta_record {"scenario": menu|level|win|lose|map|promotion|bonus|market|tutorial, "feel": on|off}` (Play Mode'da, kare kare, `events.json`
  işaretleri `open/score/medal/coins/continue/devam/map/unlock/league/spin/stop/tap/buy/fail`); kayıt sırasında UI kamerasının `cullingMask`'ı açılır (sahnede 0).
  Toplu: `Tools/meta_record.sh [record|videos|all] [an …]` → `docs/screenshots/2026-10-03-meta-<an>-{side.mp4,sheet.jpg}` (Pota sütunu `onizleme/` stilinden) ve `…-meta-all.mp4`.
- **Testler:** EditMode `MetaStyleTests`, `MetaAssetsTests`, `MetaDirectorTests`, `WinSequenceTests`, `MapLayoutTests`, `MapProgressTests`, `ReelSpinTests`,
  `BonusRulesTests`, `MetaRecordToolTests`; PlayMode `MetaPlayTests` (23 senaryo). Blender `tests/test_vfx.py MetaStillTests`. 2026-10-03 gece (inceleme düzeltmeleri sonrası): EditMode 474/474, PlayMode 186/186.
- **Dersler:** 400 üst üste binen, hepsi ayrı dokulu UI elemanı (harita yılanı) kök canvas'ın derinlik sıralamasını bozdu (başlık düğümlerin altına düştü):
  parçalar sprite paylaşır, içerik iç içe `Canvas`ta. `ColorHuntHud.CircleSprite` saydam dolguyu siyah boyar (halka için `MetaStyle.Ring`); küçük (64 px)
  gradyan `PanelSprite` `Sliced` tipte hiç çizilmedi (`Simple`). `ScreenSlide.Show` sayfayı en üste alır: sekme çubuğu/şerit her sekme değişiminde yeniden
  yükseltilir. `kahraman-maske.png` parlaklık maskesi (parça başına gri seviye), alfa değil.
- **İnceleme düzeltmeleri (2026-10-03 gece):** ödül parası `ShowWin`'de kayda yazılır, uçuş sayacı oynatır (`ShowCoins`); market para sayısını yönetmenin kaydından okur (`Bind(coins)`, sekmede `Sync`); her şerit eylemi önce açık pencereleri kapatır (`CloseWindows`); en iyi skor bölüm başına (`MetaRules.Win` yeni bölümde 0); son bölümde harita yalnız yeşil + madalya; `MetaStyle` sprite önbelleği; kapalı hâlde Pota'nın kendi hareketleri (`WinSequence.Pota`, market zıplama/sallanma/kutlama); M3 rozet ezilmesi/parıltı/dönüş (`CoinBadgeFx`); kayıt aracı hata yakalar. Ertelenen küçükler U84–U95.
- **Tuna'nın kararı (2026-10-03):** 11 anın hepsi kabul; nefes alıp verme (ana görsel ve market kahramanı) istenmedi ve kaldırıldı (`MetaCurves.Breath` ve `Feel.Breath` silindi).
- **Dürüst notlar:** teslim belgesinin §6'sı (vitrin sınırları: gerçek bölüm yok, market kısmi, saat hücresi kalp, Pota sütunu hareketsiz).
