# Renk avı (Color Hunt) — Unity tasarım denemesi (2026-09-22)

Tuna'nın isteği: Pota'nın `renk-avi` dalındaki 2. aşama (renk avı) Unity'de, Unity'nin efekt gücüyle yeniden
kurulsun — patlamalar daha etkili, tasarım daha düzgün. **Uygulama Unity'de kalır**; Pota projesine bir şey
taşınmaz. Tuna Unity'de bakıp beğenirse sonra taşınır.

Oturum etkileşimsiz olduğu için aşağıdaki kararlar sorulmadan verildi; hepsi ayar dosyasından (`ColorHuntSettings`)
değiştirilebilir. Yanlış olanı Tuna söyler, düzeltilir.

## 1. Kurallar (Pota `src/renkAvi.ts` birebir)

- 7×7 dolu ızgara, 5 renk (kırmızı, mavi, yeşil, sarı, mor — Pota `RENKLER` sırası ve tonları), her blokta can.
- Dokun → can kadar top harcanır, blok patlar; yetmiyorsa hiçbir şey olmaz, rakam kırmızı yanar ve blok titrer.
- Patlayanın üstündekiler iner, boşluklara tepeden rastgele renk/can blok girer ("kumar"). Yan yana / alt alta
  3+ aynı renk bedava patlar, zincir sürer.
- Sol/sağ ok: tahta 90° döner, kayma yok, bedava, sınırsız.
- Hedef: 22 kırmızı + 16 mavi. Cephane 158 (bölüm 20'nin ortanca basketi). Can 3–14.
- Rastgelelik tohumlu (mulberry32 portu): aynı tohum aynı tahta. Aynı algoritma, Pota testleri C#'a taşınır.

## 2. Görsel dil

- Portre telefon çerçevesi; kamera tahtayı ekrana sığdırır (en/boy neyse).
- Tahta: lacivert plaka + turuncu yuvarlatılmış çerçeve (Pota'daki 3B pano). Bloklar yuvarlatılmış küpler,
  yüzeyde Pota'nın `blok-a.jpg` (beyaz üretilmiş, kodda renklendirilir), üstünde kalın beyaz rakam ve koyu
  kenarlık. Yönlü ışık + yumuşak gölge: bloklar plakaya gölge düşürür (derinlik).
- Arka plan: Pota'nın açık mavi gökyüzü degrade'i; alt kuşakta fırlatıcı şeridi (toplar buradan çıkar).
- HUD (uGUI, Pota dili: lacivert/altın): sol üst "CEPHANE", sağ üst "HEDEF" paneli (renk karesi + kalan,
  0 olunca yeşil ✓), altta iki yuvarlak ok düğmesi (`ok-a.png`), ortada ipucu.
- Yazı tipi: Arial Rounded Bold (Pota'nın Fredoka'sına en yakın sistem fontu).

## 3. Efekt tasarımı (Unity'nin katkısı — asıl istenen)

Her efekt `ColorHuntSettings` ile ayarlanır; parçacık sistemleri sahnede `Effects` altında durur (Inspector'dan
ellenebilir), oyun kodu `Emit` ile renk vererek tetikler.

- **Salvo:** dokunulan bloğa alt şeritten canı kadar (görsel tavan 24) basketbol uçar; her top iz (TrailRenderer)
  bırakır, varınca blok beyaz flaş + ezilme (squash) + küçük renkli kıvılcım.
- **Blok patlaması (asıl gösteri):**
  1. beyaz flaş sprite + genişleyen halka (additive),
  2. bloğun renginde **3B parçalar** (mesh parçacık: küçük yuvarlatılmış küpler, dönerek, yerçekimiyle, plakaya
     çarpınca sekmez ama kamera önüne doğru saçılır),
  3. beyaz/sıcak kıvılcım çizgileri (stretched billboard),
  4. kısa nokta ışığı (bloğun renginde, 0.15 s),
  5. komşu bloklara dalga: patlayanın 4 komşusu 0.12 s ezilip geri gelir,
  6. kamera sarsıntısı + hit-stop (0.05 s), sayı popup'ı ("−N" cephane).
- **Zincir (3+):** aynı efekt, derinlikle büyür: daha çok parça, daha güçlü sarsıntı, daha uzun hit-stop; "×N"
  kombo yazısı ekranda büyüyerek; ses tizleşir (SynthSfx).
- **Düşüş:** sabit ivme (bir hücre 0.25 s), inince ezilme + toz pufu; tepeden gelen yeni bloklar çerçevenin
  üstünden düşer.
- **Döndürme:** 0.34 s ease-out, çerçeve sabit, tahta döner; oku basınca düğme çöker.
- **Yetmez:** blok yatay titrer, rakam 0.3 s kırmızı, "N top gerek" popup.
- **Kazanma:** "Hedef tamam!" + konfeti yağmuru (5 renk); **kayıp:** "Top bitti" / "Hamle kalmadı".
- Post-processing: mevcut Global Volume (Bloom 1.0 / eşik 0.9) — flaş ve kıvılcımlar HDR renkle bloom'a girer.

## 4. Mimari (`Assets/_Project/Scripts/ColorHunt/`, namespace `Project.ColorHunt`)

| dosya | iş |
|---|---|
| `ColorHuntRules.cs` | saf kural: `Board`, `Block`, `Step`; `Create`, `Break`, `Rotate`, `FindMatches`, `ResolveChains`, `Won`, `HasMove`; `Mulberry32` |
| `ColorHuntSettings.cs` | ScriptableObject: boyut, renkler, can aralığı, hedefler, cephane, süreler, efekt/juice genlikleri |
| `ColorHuntGame.cs` | durum makinesi + animasyon kuyruğu (salvo / adım / dönüş / bekleme), HUD'a olaylar |
| `BlockView.cs` | tek bloğun görseli: mesh, renk (MaterialPropertyBlock), rakam, ezilme/flaş/titrem |
| `BoardBuilder.cs` | çerçeve, plaka, fırlatıcı şeridi, ışık; `RoundedBoxMesh` |
| `BallSalvo.cs` | uçan basketbollar (havuz, iz) |
| `ColorHuntFx.cs` | parçacık sistemlerinin sahibi: `Explode(pos, color, depth)`, `Puff`, `Sparkle`, `Confetti` |
| `ColorHuntHud.cs` | uGUI; `ColorHuntJuice.cs` sarsıntı/hit-stop/popup/ses (Basket'in `CameraShake`, `TimeScaleController`, `PopupText`, `SynthSfx`'i yeniden kullanılır) |
| `ColorHuntInput.cs` | Input System: dokunma/fare → raycast → blok |
| Editor: `ColorHuntSceneTool.cs` | `ai_colorhunt_build_scene` sahneyi sıfırdan kurar; `ai_colorhunt_play` Play Mode'da dokun/döndür/otomatik oyna/durum (ekran görüntüsü için yavaş çekim) |
| Tests: `ColorHuntRulesTests.cs` | Pota `arac/renk-avi-test.ts` karşılıkları |

## 5. Sınama

EditMode testleri (`run_tests`), sahne kurulumu, Play Mode'da otomatik hamlelerle ekran görüntüleri
(`docs/screenshots/colorhunt_*.png`), konsolda hata yok.

## 6. Kapsam dışı

Pota'ya taşıma · basket aşamasıyla akış bağı · 40 bölüm · ses tasarımı (sentez yeter) · mobil build.
