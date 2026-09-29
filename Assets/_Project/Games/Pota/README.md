# Pota (Unity denemeleri)

Pota / "Swish & Smash" asıl olarak three.js ile yazılmış bir iOS oyunu (`~/projects/Pota`, salt okunur, kurallar ve görseller oradan). Bu klasör, oyunun bölümlerinin Unity'deki yeniden yapımlarını tutar. Pota'nın Unity'ye geçip geçmeyeceğine Tuna karar verecek. Geçerse bu klasör olduğu gibi yeni bir Pota Unity projesine taşınır (`.meta` dosyalarıyla birlikte kopyala, GUID'ler korunur). O projede `Core/` da gerekir.

Pota üzerinde çalışırken bu dosyayı oku ve güncel tut. Genel kurallar ve köprü notları kök `CLAUDE.md`'de.

## Klasör ve assembly'ler

- `Scripts/` → `Pota.Runtime` (`Project.Runtime`'a bağlı; ad alanları `Project.Basket`, `Project.Basket.Visual`, `Project.ColorHunt`)
- `Editor/` → `Pota.Editor` (sahne kurma, tarama, kayıt, doku, karakter araçları; ad alanı `Project.Editor.AITools`)
- `Tests/EditMode` → `Pota.Tests.EditMode`, `Tests/PlayMode` → `Pota.Tests.PlayMode`
- `Scenes/` (Basket, ColorHunt: ikisi de Build Settings'te), `Settings/` (ayar asset'leri + fizik malzemeleri), `Art/` (Pota'dan kopyalanan görseller, `Art/Generated` üretilmiş basketbol dokusu, `Art/Fonts`, `Art/Mesh`, `Art/Anim`), `Prefabs/` (PlayerCharacter, PotaHeroToon), `Art/Characters/` (Blender teslimleri), `Anim/` (AnimatorController), `Shaders/` (`Project/TextDepth`)
- Ortak juice bileşenleri (`CameraShake`, `TimeScaleController`, `PopupText`, `SynthSfx`) artık Core'da, ad alanı `Project.Juice`. VFX: `Project.VFX.VfxService` (Core). Kod içinden tip adı: `"Project.Basket.BasketRound, Pota.Runtime"`.
- Oyun profili: `PotaProfile.asset` (Game view 1170×2532 = yeni iPhone oranı; ürün adı "Pota Unity", bundle id `com.tunaaltin.pota.unity`, yani gerçek Pota'dan (`com.tunaaltin.pota`) ayrı bir uygulama; Apple takımı `BJPL5XNDLV` (Pota'nın Xcode projesinden); sahneler Basket, ColorHunt).
- Testler: EditMode kurallar (`AimMapper`, `TrajectorySolver`, `LevelRules`, `RoundRules`, `ColorHuntRules`); PlayMode `BasketRoundPlayTests` (2 atış), `ColorHuntPlayTests` (3 dokunuş: animasyon biter, cephane düşer, HUD yetişir, hedef azalır). Core'daki `SceneBootTests` iki sahneyi de açıp 3 sn hatasız çalıştırır.
- İlk karakter denemesi (25 Eylül, git'te saklı): `Assets/_Project/Experiments/PotaCharacterExperiment/` (menü `Pota/Character/Build and bake animations`).

## Basket turu (Pota "Faz A" yeniden yapımı, 2026-09-19)

Sahne `Scenes/Basket.unity`, kod `Scripts/Basket/` (saf kurallar: `AimMapper`, `TrajectorySolver`, `LevelRules`, `RoundRules`; bileşenler: `BasketRound` durum makinesi, `Ball`/`BallPool`, `Hoop`, `BasketCage`, `HoopColumnBuilder`, `RoundCamera`, `BasketJuice`, `BasketHud`, `PlayerActor`, `Visual/*`), ayarlar `Settings/BasketSettings.asset` (tüm sayılar orada, koda gömme). Tasarım `docs/superpowers/specs/2026-09-18-basket-round-design.md`, plan `docs/superpowers/plans/2026-09-18-basket-round.md`, ölçüm `docs/measurements/2026-09-19-basket-sweep-L1.md`, görüntüler `docs/screenshots/`. (docs/ repo kökünde, eski yolları tarihsel kayıttır.)

- Araçlar: `ai_basket_build_scene` (sahneyi sıfırdan kurar; katmanlar BallFlight/BallDrop/Hoop/Slab, fizik malzemeleri, sanat referansları), `ai_basket_sweep` (Play Mode'da atış taraması, action start/status/cancel), `ai_basket_ball_texture` (basketbol dokusu), `ai_basket_record` (kare kare kayıt). Menü: `AI Tools/Basket/...`.
- Fidelite notları: uçuş 1 sn parabol, 0.09 sn top aralığı, ×2/+1 kuralları, bölüm tablosu, sepet sayımı orijinalle aynı. 3B'ye özel devir: uçan top pota katmanıyla çarpışmaz, z=0'da Drop fazına geçer (ileri hız ×0.1); 0.45 m üstünden gelen top yanal hız ×0.3 ("panoya çarptı"), 1 m üstünden gelen yana itilir ("panoyu aştı"). Geçiş: yukarıdan giren top düzlemin altında R−r/2 içindeyse sayılır. 1. bölüm taraması: min 12 / medyan 19 / maks 28 (orijinal 12 / 21 / 32).
- Denemek için: Basket sahnesini aç, Play, fareyle aşağıdan yukarı sürükle (sola/sağa pota seçer, uzunluk güç); "Tekrar" düğmesi turu sıfırlar. Bölüm değiştirmek: `Round` objesindeki `Level` alanı.
- **Kare kare inceleme (2026-09-23, Tuna: "video kaydına al, frame frame izle"):** `ai_basket_record` (`Editor/BasketRecordTool.cs`) Play Mode'da programatik atışı PNG dizisi olarak kaydeder (`Temp/record/<ad>/`, ana kamera offscreen RenderTexture'a; kare adında fizik zamanı ve sepet sayısı). Araç kendi küçük yüzen Game view'ını açar, editörü Pause'a alır ve her tick `EditorApplication.Step()` ile tam bir oyun karesi ilerletir (`Time.captureDeltaTime` 1/30). Kolaj/mp4 dışarıda ffmpeg ile (`docs/screenshots/basket_round_L1_*.mp4`, `basket_sheet_*.png`). Bulunan ve düzeltilen: teras levhası oyuncu gizlenince kalıyordu → `PlayerActor.AlsoHide`. Sonuç: uçuş, üst geçiş, ×2 çoğalma, +1, sepete dolma ve kamera inişi temiz; 26/25 sepet, 8 üst geçiş.
- Sıradaki adaylar: üst kuyruk ayarı (alt sıra geçişleri / CloneScatter), telefon dokunmatik testi ve build, Faz B (bulmaca) entegrasyonu.

## Renk avı (Pota 2. aşama denemesi, 2026-09-22)

Sahne `Scenes/ColorHunt.unity`, kod `Scripts/ColorHunt/` (saf kural `ColorHuntRules` = Pota `src/renkAvi.ts` portu, mulberry32 dâhil; `ColorHuntGame` durum makinesi + animasyon kuyruğu; `BlockView`, `BoardBuilder`+`RoundedBoxMesh`, `ColorHuntFx` (tek parçacık sistemi seti, `Emit` ile renk), `BallSalvo`, `ColorHuntHud` (uGUI, üretilmiş sprite'lar), `ColorHuntJuice` (Core juice bileşenleri), `ColorHuntCamera` (en/boya göre sığdırma), `ColorHuntInput` (Input System pointer)). Ayarlar `Settings/ColorHuntSettings.asset` (bölüm 20: 7×7, 5 renk, can 3–14, cephane 158, hedef 22 kırmızı + 16 mavi; efekt/juice sayıları). Tasarım `docs/superpowers/specs/2026-09-22-color-hunt-design.md`, görüntüler `docs/screenshots/colorhunt_*.png`. Testler `Tests/EditMode/ColorHuntRulesTests.cs` (8/8).

- Araçlar: `ai_colorhunt_build_scene` (sahneyi sıfırdan kurar, Game view'ı 1080×1920'ye alır — Core'daki `GameViewPortrait`), `ai_colorhunt_play` (Play Mode: status / tap {cell} / rotate {dir} / auto {moves} / slowmo {scale, seconds} / retry). Menü: `AI Tools/Color Hunt/...`.
- **Revizyon 2026-09-23 (Pota commit 71f8b9e/f55775d):** bloklar renk + basket eşyası + rakam (`ColorHuntSettings.ItemIcons`, `Art/esya-*.png`; eşya yüzün üst yarısında ölçek 0.62, rakam önde ve altta 0.56 — Pota `etiketleriYaz` düzeni), hedef panelinde eşya ikonu. Unity ekstrası: sayılan bloğun eşyası patlayınca HUD'daki hedef kutusuna uçar ve ikonu zıplatır (`ColorHuntFx.FlyItem`, `ColorHuntHud.TargetScreenPoint/PopTarget`). Quad primitive zaten −z'ye (kameraya) bakar; 180° çevirme mirror + cull yapar.
- Denemek için: ColorHunt sahnesini aç, Play, bloğa tıkla (bas-sürükle-bırak seçimi), alttaki oklar döndürür. Pota projesine taşınmadı; Tuna Unity'de bakıp karar verecek (patlama/efekt hissi, tahta boyu, HUD).

## Oyuncu karakteri (2026-09-25)

Karakter hattının kendisi atölyenin parçası (`Tools/character/README.md`, özet `docs/2026-09-25-character-pipeline.md`). Pota'daki sonuç: `Prefabs/PlayerCharacter.prefab` (rig `Art/Mesh/player_rig_a.glb` + `.avatar.asset`, controller `Anim/PlayerCharacter.controller`), klipler `Art/Anim/player_*.fbx`. `BasketSettings.PlayerPrefab` buna bağlı, `PlayerActor` Shoot'u `PlayerShootClipStart`'tan oynatır. Koşu/smaç klipleri controller'da var, oyun tetiklemiyor.

### Yeni toon kahraman (2026-09-29, karşılaştırma için)

`Prefabs/PotaHeroToon.prefab` (+ `PotaHeroToon_Toon.mat`, controller `Anim/PotaHeroToon.controller`, rig `Art/Characters/pota-hero/pota-hero_rig.glb` + `.avatar.asset`). Nasıl yapıldı: aynı `apose2` ön görselinden Rodin Gen-2 modeli → Blender `character_model` (8400 üçgen, kapalı mesh, 12 renkli doku; teslim `Art/Characters/pota-hero/`) → Uthana otomatik iskelet → `ai_character_setup` → `ai_toon_material` (Core `Project/Toon`: iki tonlu gölge, kenar parlaması, dış hat). Aynı `player_*` kliplerini oynar; kemik yükseklikleri eski oyuncuyla ±2 cm. Karşılaştırma görüntüleri `docs/screenshots/2026-09-29-pota-hero-toon{,-close}.jpg`, model raporu `~/projects/Blender MCP/docs/reports/character-model-comparison.md`.

- **Oyundaki oyuncunun yerine geçmedi.** Değiştirmek Tuna'nın kararı: `BasketSettings.PlayerPrefab`'i `PotaHeroToon` yapmak yeterli (ölçek ve klip başlangıcı aynı).
- Bilinen kusurlar: saçta iki tonlu yama (Blender backlog B10), parmaklarda koyu dış hat çizgileri.
- `ai_character_setup` (`Editor/CharacterSetupTool.cs`) varsayılanları Pota'ya göre (klip adları `player_*`, `BasketSettings.PlayerPrefab` ataması), o yüzden burada duruyor. İkinci bir oyun karakter isterse varsayılanları parametreye çevirip Core'a taşı.

## Telefonda deneme (iPhone)

1. Claude derler: `ai_build {"action":"start","game":"Pota"}` → `Builds/Pota-ios/Unity-iPhone.xcodeproj` (ilk derleme iPhone moduna geçişle birkaç dakika, sonrakiler saniyeler; 2026-09-28'de Unity derlemesi ve imzasız `xcodebuild` ikisi de başarılı). Tek sahne için `"scene":"ColorHunt"`.
2. Tuna kurar: `open Builds/Pota-ios/Unity-iPhone.xcodeproj` → iPhone'u kabloyla bağla → Xcode'un üst çubuğunda hedef olarak telefonu seç → ▶ (Run). İmza otomatik, takım Pota'nınki. İlk kurulumda telefonda Ayarlar > Genel > VPN ve Cihaz Yönetimi'nden geliştiriciye güvenmek gerekebilir.
3. Uygulama "Pota Unity" adıyla, gerçek Pota'nın yanına ayrı uygulama olarak kurulur ve Basket'le açılır. Üstteki "→ ColorHunt" düğmesi (yalnız test derlemesinde) sahneler arasında geçer.
