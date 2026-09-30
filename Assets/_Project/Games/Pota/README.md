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

## PotaFlow: sepetten tahtaya geçiş (2026-09-29/30)

Tek sahne `Scenes/PotaFlow.unity`: Basket kulesi orijinde, terasın arkasında turuncu çatı sahası, sahanın sonunda dev pota ve içinde Renk Avı tahtası (yalnız ötelenmiş, `BoardAnchor` (0; 8,5; 30)). Basket turu bitince kamera kesintisiz uçar, sepetteki toplar dev potanın filesine akar ve cephane olur. Tasarım `docs/superpowers/specs/2026-09-29-pota-flow-transition-design.md`, plan `docs/superpowers/plans/2026-09-29-pota-flow-transition.md`, görüntüler `docs/screenshots/2026-09-29-potaflow-*` (A ve B kare levhaları, Pota | A | B yan yana levha ve video).

- **Denemek için:** PotaFlow sahnesini aç, Play, aşağıdan yukarı sürükleyerek at (bölüm 20). Tur bitince geçiş başlar; geçiş sırasında ekrana dokunmak kalan kısmı ≤ 0,35 sn'de bitirir. Tahta bitince sonuç kartındaki "Tekrar at" kamerayı nişan pozuna geri uçurur ve Basket turu yeniden başlar.
- **Aday A seçildi (Tuna, 2026-09-30), varsayılan** (`Settings/PotaFlowSettings.asset` → `Candidate`). **A "Akan toplar":** 14 görünür top kafesten fışkırır, kahramanın yanındaki **top sepetine** (`BallBin`, kuledeki kafesin tel örgü tarzı, `BinPosition` (1,4; 0; 7,5)) eğik kavisle düşer, en çok 12'si sepette yığılır; cephane paneli her inişte payını ekler; kahraman sepete döner. Dev potanın filesi boş kalır (dekor). Tuna'nın gerekçesi: "adamın yanına bir sepet koysak, toplar oraya dolsa mantıklı olur". **B "Sade"** ayarda kalır (top akışı yok, rozet kamerayla gider).
- **Zaman çizelgesi** (gerçek zaman, `FlowTiming`): 0–0,3 kafeste "N TOP" + kafes ağzında parlama + FOV vuruşu; 0,25–0,77 toplar çıkar (0,04 sn arayla); 0,6'da kahraman, teras ve sepet görünür; kamera iki vuruşta gider (0,3–1,1 kuleden yükselip yan manzaraya (8,5; 4; 4,5): kahraman + sepet, 1,35'e kadar bekler, 1,35–1,95 bakışı önden giderek tahtaya); 0,9–1,25 kahraman sepete döner; 0,9'da cephane paneli girer, 0,95–1,45 toplar sepete iner ve sayaç artar; 1,75'ten tahta ortadan dışa halka halka zıplar, çerçeve parlar; 1,9–2,25 hedef paneli ve oklar girer; 2,5 hazır.
- **Spec'ten ayar farkları (kayıtlardan sonra, gerekçeleriyle):** toplar fileye değil kahramanın sepetine (Tuna); inişler kamera kahramandayken (0,95–1,45); 25 yerine 14 daha küçük top (r 0,19; 25 top kameraya yakın tek bir "tırtıl" zincir gibi görünüyordu); cephane paneli 1,90 yerine 0,9'da (sayım görünmüyordu); manzara (13; 7; 17) yerine (8,5; 4; 4,5), bakış kahraman ve sepete (sepet ve kahraman birbirini kapatmıyor, Basket görüntüsünde sepet kahramanın yanında); ikinci kamera vuruşunda bakış önden gider (`LookLead`, boş gökyüzü karesi kalmadı); ipucu yazısı PotaFlow'da gizli (dev potanın filesinin üstüne biniyordu). Görüntüler `docs/screenshots/2026-09-30-potaflow-basket-{sheet.jpg,side.mp4}` (Pota | eski A | yeni A).
- **Kod** (`Scripts/Flow/`, ad alanı `Project.Flow`): `FlowTimeline` (saf; her t için top çıkış/iniş, paylar toplamı tam N, kamera ilerlemesi, halka ölçeği, HUD, atlama hızı), `CameraFlight` (saf Catmull-Rom bakış+göz yolu), `GiantHoop` (cam pano, turuncu çerçeve, çember R 1,6, 6 halkalı file, direk), `BallBin` (kahramanın top sepeti), `BallStream` (havuzlu görsel toplar, `IBallReceiver`'a deterministik yol, her iniş tek olay), `BoardReveal`, `CourtBuilder` (Pota `saha.ts` #d9773a zemin + beyaz çizgiler, kodla çizilen doku), `FlowBadge`, `PotaFlowSettings`, `PotaFlowDirector` (Basket → Geçiş → Bulmaca → Sonuç → Dönüş; faz başına girdi, kamera bileşeni, sis 26–115 → 60–220, HUD). Mevcut koda küçük kancalar: `ColorHuntGame.Prepare/ShowAmmo/Begin/AutoStart`, `ColorHuntCamera.Anchor/FitFor`, `ColorHuntHud.SetIntro/RetryOverride/ShowHint`, `BasketHud.ShowResultPanel`, `RoundCamera` (etkinleşince bulunduğu yerden devam, `LookPoint`), `PlayerActor.Reveal`, Core `TimeScaleController.Clear`.
- **Araçlar:** `ai_potaflow_build_scene` (sahneyi Basket ve Renk Avı yardımcılarıyla kurar, Build Settings'e ve `PotaProfile`'a ekler), `ai_potaflow_record` (`{"candidate":"A"|"B","count":158,"shoot":true}`: bölüm 20'yi programatik atışla oynatır, geçişi 30 fps PNG olarak `Temp/record/<ad>/` altına yazar; UI dâhil), `Tools/frame_sheet.py` (kare klasörlerinden zamanlı levha). Kayıtta overlay UI kameraya düşmez: `FrameRecorder` overlay canvas'ları kayıt süresince kamera kipine alır; rozet bu yüzden ekran oranı çapasıyla yerleşir.
- **Pota'dan alınan sayılar:** dev pota ölçüleri `src/sahaYerlesim.ts` DEV_POTA ve `src/pota.ts` (çember R 1,6, panonun altında ve öne R+0,6, 6 halkalı file), zemin rengi `src/saha.ts`, akış `src/akis.ts` (`fazABitti` → `fazBBasla`, cephane = sepet sayısı), kamera `src/kameraYonetmeni.ts` (1,4 sn karışım; bizimki ~2,5 sn, dokununca kısalır). Pota karşılaştırması Pota'nın **kopyasından** kaydedildi (scratch'te rsync + vite + kendi Playwright betiğimiz); Pota klasörüne hiçbir şey yazılmadı.

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
