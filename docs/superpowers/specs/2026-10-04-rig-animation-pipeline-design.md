# İskelet ve animasyon hattı — tasarım (2026-10-04, yol haritası 3)

Tuna (2026-10-04): "stüdyonun eksiksiz oturması şart"; oyun yapılmıyor, oyunlar yalnız stüdyo araçlarını denemek için. Bütçe tavanı
**15 $** (bu alt proje). Teknik kararlar bana bırakıldı. Araştırma: `docs/reports/2026-10-04-rig-anim-research.md` (özeti §2'de).

## 1. Boşluk

Model hattı bitti (kahraman v2, 8 480 üçgen, toon). İskelet ve hareket ise 2026-09-25'teki deneme düzeyinde:
- Servis çağrıları `Unity MCP/Tools/character/rep.py` ile, copy'nin harcama defterine bağlı değil.
- Hareketler Uthana'nın Y Bot'una üretilip Unity **Humanoid** ile kahramana aktarılıyor. Humanoid kemik ölçeğini atar: squash/stretch ve
  "smear" imkânsız. Şut iyi, koşu sıradan bir tempo, smaç zayıf.
- Blender'da animasyon adımı yok: temizleme, abartma, bekleme, hazırlık/sekme, ayak kilidi yapılmıyor. Yapay zekâ hareketleri gerçekçi
  hareket kaydı tarzında; Supercell hissi ancak bu adımdan gelebilir.
- Unity'de ikincil hareket (yaylanma, sarkma, ezilme) yok; `ai_character_setup` Pota'da ve Pota varsayılanlarıyla.
- Ölçü yok: "bu hareket iyi mi" sorusuna sayı veren bir araç yok.

## 2. Araştırmadan kararlar

1. **İskelet:** Uthana `create-character-v1` (Replicate, ~0,01 $, Mixamo adları, parmaklar) birincil. Büyük kafa Uthana'nın kendi
   belgelerinde zayıf nokta; bu yüzden Blender'da **iskelet puanı** (`rig_check`) ve **ücretsiz yedek**: aynı iskelet, Blender'ın kemik ısısı
   ağırlıklarıyla yeniden ağırlıklandırma (`rig_reweight`; kafa tamamen kafa kemiğine). İkisi aynı zor pozlarda yan yana puanlanır.
   Tripo/Meshy (fal) yeni hesap ve anahtar ister; bu turda yok (gerekirse sonra).
2. **Hareket:** Uthana text-to-motion v2 (Replicate, kuruşlar, 20 fps, Y Bot). Ücretsiz kontrol grubu: Quaternius UAL (CC0) indirilebilirse.
3. **Stil Blender'dan gelir:** her üretici gerçekçi hareket kaydıyla eğitilmiş. `anim_stylize` tarifi: ayak kilidi → anahtar azaltma →
   zaman bükme (beklemeler, hızlı geçişler) → hazırlık ve sekme → bölgeye göre abartma → squash/stretch yardımcı kemikleri → döngü kapatma
   → yerinde (root) ayıklama. Her parametre klip başına tarif dosyasında; Tuna'nın notu sayıya döner.
4. **Unity Generic:** kahraman klipleri **Generic** (ölçek eğrileri, kesin el pozu); Humanoid yalnız hazır kütüphane klibi ödünç almak için.
   Teslim **FBX** (Unity'nin kendi içe aktarıcısı: Generic, klip başına döngü ayarı kalıcı); glb önizleme/three.js için yanında.
5. **İkincil hareket kendi kodumuz:** `SquashStretch` (zıplama/iniş/vuruşta hacmi koruyan ölçek) ve basit `SpringBone` (kafa süsü, saç,
   kulak gecikmesi). Dış paket yok.
6. **Ölçü:** `anim_metrics` (bekleme oranı, keskinlik = tepe hız / ortalama hız, sekme yüzdesi, zıplama ezilmesi, koşu salınımı) ham ve
   stilize klipte; hedef aralıklar zanaat normları (araştırma §5) — Squad Busters kaydı edinilebilirse oradan ölçülüp değiştirilir.

## 3. İş bitince

1. **copy:** `cli/iskelet.ts` (Uthana iskelet) ve `cli/hareket.ts` (Uthana text-to-motion), harcama defteri (`--tavan`, `--kurugidis`) ve
   köken kaydıyla; `Unity MCP/Tools/character/rep.py` emekli (README yönlendirir).
2. **Blender** (`tools/bl/`): `rig_check`, `rig_reweight`, `anim_retarget` (Mixamo adlı kaynaktan kahramanın iskeletine), `anim_stylize`
   (tarif), `anim_metrics`, `character_animated` (kahraman + klipler → `kind: character` FBX+glb teslimi; manifest'te `clips_meta`
   `[{name, frames, fps, loop}]` ve `import: {rig: "generic"}`), hepsi testli.
3. **Unity:** `ai_import_asset` türe göre içe aktarma ayarı (U1: FBX `kind: character` → manifest'teki `import.rig`, klip döngüleri
   `clips_meta`'dan; U4: meshsiz teslimde kemik sayımı); `ai_character_setup` Core'a taşınır, Generic/Humanoid seçilebilir, oyuna
   bağlı varsayılan kalmaz (Pota çağrıları aynı sonucu verir); Core `SquashStretch` + `SpringBone`; `ai_anim_showcase` (kahramanı
   klipleri sırayla oynatan vitrin sahnesi).
4. **Kanıt (Pota kahramanı, 8 klip: bekleme, koşu, şut, zıplama, zafer, yenilgi, darbe alma, sevinç):**
   `docs/screenshots/2026-10-04-anim-*`: her klip için **Ham yapay zekâ | Stilize** (varsa **| CC0 kontrol**) yan yana video ve
   kare levhası + metrik tablosu (`docs/reports/2026-10-04-anim-metrics.md`). Kahraman oyunda değişmez (Basket turu eski klibiyle kalır);
   kanıt `Experiments/` vitrininde.
5. Harcama defterde, 15 $ altında (tahmin 1–3 $). Testler yeşil. Unity/Blender öne çağrılmaz, arayüzlü Blender açılmaz.

## 4. Akış (bir karakter için)

```
copy  iskelet.ts  model.glb ─► rig.glb (Uthana)                 hareket.ts "prompt" ─► clip_ybot.fbx (Uthana, Y Bot)
                                   │                                        │
Blender  rig_check(rig) ─► puan; düşükse rig_reweight ─► rig_ok.blend        │
         anim_retarget(clip_ybot, rig_ok) ─► ham klip (kahramanın iskeletinde) ◄┘
         anim_stylize(ham, tarif) ─► stilize klip ; anim_metrics(ham|stilize) ─► sayılar
         character_animated(rig_ok, [klipler]) ─► export/<id>.fbx + .glb + manifest (clips_meta, import.rig=generic)
Unity    ai_import_asset ─► Generic, döngüler ─► ai_character_setup (controller, toon) ─► SquashStretch/SpringBone ─► vitrin
```

## 5. Parçaların ayrıntısı

### 5.1 `rig_check(path, stress=True)`
- Eklem/işaret uyumu: kafa kemiği başlangıcı boyun dar noktasında mı (mesh kesit genişliği minimumu), omuz/dirsek/diz kesit dönüm
  noktalarına yakın mı (boyun %'si olarak sapma).
- Ağırlık taşması: kafa bölgesindeki (boyun üstü) köşelerin kafa/boyun dışı kemiklerden aldığı ağırlık payı; ellerin gövdeden aldığı pay.
- Zor pozlar: kollar yukarı, kollar çapraz, derin çömelme, kafa 45° dönük — her pozda yüzey kendi içine geçiyor mu (kendi kesişen üçgen
  sayısı yaklaşık, BVH) + tek PNG.
- Çıktı: `{score 0–1, landmarks, bleed, intersections, png}`; eşik altında `rig_reweight` önerisi.

### 5.2 `rig_reweight(path, head_rigid=True)`
İskeleti korur, ağırlıkları `ARMATURE_AUTO` (kemik ısısı) ile yeniden hesaplar; `head_rigid` ile boyun üstü köşeler yüzde yüz kafaya.
Kapalı mesh şart (model hattı garanti ediyor). Aynı `rig_check` ile karşılaştırılır; iyi olan seçilir.

### 5.3 `anim_retarget(source, target, clip=None)`
Mixamo adlı iki iskelet arasında: her kemiğin dünya dönüşü dinlenme pozuna göre farkla aktarılır; kalça yüksekliği bacak boyu oranıyla
ölçeklenir; kaynakta olup hedefte olmayan kemik atlanır (raporlanır). Blender 5 yuvalı aksiyonları (`bpy_extras.anim_utils`).
Test: aynı iskelete aktarım kimlik dönüşümü verir; uzun bacaklı kaynaktan kısa bacaklı hedefe ayak yerden kopmaz (en alçak ayak ≈ 0).

### 5.4 `anim_stylize(path, clip, recipe)` — tarif `recipes/anim/<klip>.json`
Sıra ve parametreler:
1. `foot_lock {height, speed, blend}`: temas (ayak yüksekliği ve yatay hız eşikleri, ≥2 kare) → kilit konumu; iki kemikli IK numpy ile.
2. `reduce {error}`: tüm kemiklerde birlikte uç (extrema) anahtarlar, hata eşiğiyle (pozlar kemikler arasında hizalı kalır).
3. `timewarp {beats: [[t_src, t_dst], ...]}`: parçalı doğrusal zaman bükme (beklemeler uzar, geçişler kısalır), hareketli bekleme %3–8 sürüklenme.
4. `anticipate {at, amount, frames}` / `overshoot {at, amount, settle}`: ana pozdan önce karşı poz, sonra taşma ve oturma.
5. `exaggerate {regions: {spine: k, arms: k, legs: k}}`: alçak geçirgenli eğriden sapma × k (dönüşlerde kuaterniyon log uzayında).
6. `squash {at, scale}`: `squash` yardımcı kemiği (kökün çocuğu, gövdeyi taşır) y-ölçeği, x = z = 1/√y (hacim korunur).
7. `loop {frames}`: son N kare ilk poza harmanlanır.
8. `in_place`: kökün yatay ötelemesi ayıklanır (koşu döngüsü).
Her adım ayrı fonksiyon, ayrı test; tarif boş adımı atlar.

### 5.5 `anim_metrics(path, clip)`
Kemik dünya konumlarından: bekleme oranı (ortalama eklem hızı < eşik olan kare payı), keskinlik (tepe/ortalama hız), sekme yüzdesi ve
oturma kare sayısı, kök yüksekliği salınımı / boy, gövde eğimi, ezilme (sınır kutusu yüksekliği / dinlenme). JSON + Markdown satırı.

### 5.6 Unity
- `ai_import_asset`: FBX'te `ModelImporter.animationType` = manifest `import.rig` (`generic` varsayılan, `humanoid`), `clipAnimations`
  `clips_meta`'dan (`loopTime`, ad), `importAnimation` açık; glb'de glTFast varsayılanı kalır (önizleme). Meshsiz teslimde kemik sayısı
  hiyerarşiden (manifest'teki `stats.bones` ile).
- `ai_character_setup` → `Core/Editor/AITools/CharacterSetupTool.cs`: `{model, rig: generic|humanoid, controller?, prefab?, toon?}`;
  Animator + klip başına durum (ilk klip varsayılan), geçişler ≤ 0,1 sn; Pota'nın mevcut çağrısı aynı sonucu verecek şekilde.
- `SquashStretch` (Core Juice): hedef transform'a hacmi koruyan ölçek; `Pulse(amount, duration)` ve dikey hızdan otomatik (iniş/kalkış).
- `SpringBone` (Core): kemik zinciri sarkması, sertlik/sönüm, `LateUpdate`, ölçeksiz zaman isteğe bağlı.
- `ai_anim_showcase {model|prefab, scene}`: klipleri sırayla oynatan vitrin (ad etiketiyle); kayıt `ai_anim_contact_sheet` + kare kare kayıt.

## 6. Kapsam dışı

Yüz ifadesi (blendshape/yüz kemiği), parmak animasyonunun elle iyileştirilmesi, kumaş, ses, Tripo/Meshy/HY-Motion (yeni hesap ve lisans
riski), Cascadeur (arayüzlü uygulama, odak çalar), kahramanın oyundaki kliplerinin değiştirilmesi (oyun işi, Tuna'nın seçimine bağlı).

## 7. Riskler

1. Stil boşluğu: algoritmik abartma teması/eklem sınırını bozabilir → her adım metrikle ve yan yana levhayla geçilir.
2. Büyük kafa: kollar kafaya geçebilir → `rig_check` zor pozları + klipte el-kafa mesafe denetimi (metrikte kesişme uyarısı).
3. Uthana'nın Replicate modellerinde lisans yazmıyor: atölye denemesi için sorun değil; yayına giden kliplerde Uthana'dan yazılı teyit ya da
   doğrudan API (ticari kullanım açık) — Tuna'ya rapor edilir.
4. Blender FBX dışa aktarımında çoklu aksiyon (yuvalı aksiyonlar) → ilk iş bir fikstürle doğrulanır.
