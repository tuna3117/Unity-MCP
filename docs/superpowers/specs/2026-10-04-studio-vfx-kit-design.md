# Stüdyo efekt kiti (Unity tarafı) — tasarım (2026-10-04)

Tuna (2026-10-04): "oyun yapmana gerek yok … stüdyoların bitmesi lazım", "stüdyonun eksiksiz oturması şart". Teknik kararlar bana
bırakıldı; oyunlar yalnız stüdyo araçlarını denemek için kullanılır. Bu belge stüdyonun efekt hattındaki boşluğu kapatır.

## 1. Boşluk

Blender atölyesi efektleri oyundan bağımsız üretiyor (`vfx_flipbook`: puf, halka, duman, ateş, alev, toz, hız çizgisi, çatlak, yazı;
`vfx_fracture`/`vfx_mesh`: parçalanan blok, top yarıları, madeni para). Unity `ai_import_asset` bunları içeri alıp ölçüyor. **Ama efekte
dönüşmeleri oyuna özel kodla oluyor:** Pota'da `ColorHuntFx`, `StudioParticles`, `RoyalSpecials`, `PotaFlipbook.shader`… Başka bir oyun
(Kaptan, gelecek bulmaca oyunu) aynı efekti kullanmak için bu kodu yeniden yazmak zorunda. Core'daki `VfxService` ise yalnız elle
kurulmuş yedi başlangıç efektini (hit, explosion, pickup…) biliyor; teslimden efekt üreten bir yol yok, efektlere bakmanın da (animasyondaki
`ai_anim_contact_sheet` gibi) bir aracı yok.

## 2. İş bitince

1. **Tek çağrı:** `ai_vfx_from_delivery {"folder": "<teslim klasörü>"}` bir flipbook ya da mesh teslimini havuzlu bir efekte çevirir:
   prefab + materyal + `VfxDefinition` + oyunun `VfxLibrary`'sine kayıt. Oyun kodu yalnız `VfxService.Play("fire_toon", konum)` yazar.
2. **Göz kontrolü aracı:** `ai_vfx_contact_sheet {"prefab": "…"}` efekti sabit zamanlarda (ör. 0, %15, %30 … %100) render edip tek PNG
   verir; Play Mode gerekmez, sonuç her seferinde aynıdır.
3. **Kanıt:** Pota'nın 19 teslimiyle (ham veri taşıyan şok halkası hariç) üretilen efektlerin levhası
   `docs/screenshots/2026-10-04-studio-vfx-kit.jpg` ve bir vitrin sahnesinde `VfxService.Play` ile oynatılan kayıt; çıktılar
   `Experiments/StudioVfx/` altında (oyunun klasörü değişmez).
4. Testler yeşil; ücretli çağrı yok; Unity öne çağrılmaz.

## 3. Parçalar

### 3.1 `Project/Flipbook` shader (`Core/Shaders/Flipbook.shader`)
URP, HLSL, SRP Batcher uyumlu. Parçacık köşe rengiyle çarpılan sayfa; `_Blend` (Alpha = düz alfa, Additive), `_Intensity` (HDR
çarpanı: bloom'a parlak efekt; CLAUDE.md dersi: köşe rengi 8 bit, tavan materyalde), `_Cull` kapalı, `ZWrite Off`. Kare karıştırma yok:
toon flipbook'lar kare kare okunmalı (Blender tarafı keskin karelerle çiziyor). Pota'nın `PotaFlipbook.shader`'ı bunun özel hâli;
Pota'ya dokunulmaz.

### 3.2 `IVfxPart` + `VfxInstance`
`VfxInstance` bugün yalnız `ParticleSystem` biliyor; parçacıksız bir efekt (mesh parçaları) ilk karede "bitti" sayılıp havuza dönerdi.
Yeni arayüz `IVfxPart { void Restart(); bool IsAlive { get; } void Clear(); }`; `VfxInstance` çocuklarındaki parçaları da başlatır, bitişte
onları da bekler. Mevcut efektlerin davranışı değişmez.

### 3.3 `MeshBurst` (`Core/Scripts/VFX/MeshBurst.cs`, `IVfxPart`)
Çok parçalı bir mesh teslimini (parçalanan blok, bomba kabuğu, top yarıları) patlatır. Her parça merkezden dışa (yukarı eğilimli)
`speed ± spread` hızla fırlar, yerçekimiyle düşer, döner; ömrün son `%shrink`'inde sıfıra küçülür. Hareket kapalı formülle hesaplanır
(`Evaluate(t)`: p = p0 + v·t + ½·g·t²), yani sonuç kare hızından bağımsız ve levha aracı herhangi bir anı doğrudan çizebilir.
Rastgelelik tohumlu (`seed`); `Restart` parçaları dinlenme pozuna döndürür. Ayarlar bileşende (`speed`, `spread`, `upBias`, `gravity`,
`spin`, `lifetime`, `shrink`), Inspector'dan değiştirilebilir.

### 3.4 `ai_vfx_from_delivery` (`Core/Editor/AITools/VfxFromDeliveryTool.cs`)
Girdi: teslimin Unity klasörü (`manifest.json` içinde) ya da `manifest` mutlak yolu (önce `ai_import_asset`). Seçenekler: `id`
(varsayılan teslim kimliği, `vfx-` atılır, `-` → `_`), `out` (varsayılan `Games/<Oyun>/VFX/<id>/`), `library` (varsayılan
`Games/<Oyun>/VFX/<Oyun>VfxLibrary.asset`, yoksa kurulur), `size` (m, varsayılan 1), `count`, `blend`, `intensity`, `color`.

| Teslim | Ne kurulur |
|---|---|
| flipbook, çok kare | Tek patlamalık `ParticleSystem`: `count` (varsayılan 1) parçacık, ömür = `duration_s`, Texture Sheet Animation ızgara `cols×rows`, kare eğrisi 0 → `frames` (kullanılmayan hücreler atlanır), `loop: true` ise döngü ve ömür `duration_s × 4` |
| flipbook, tek kare (yazı, çatlak, bulut, el) | Aynı sistem, sayfa en-boy oranı (`frame_px × frame_h`) `startSize3D` ile korunur; ölçek "pop" eğrisi (0 → 1,15 → 1) ve sonda solma |
| mesh, tek parça (madeni para, top) | Mesh parçacık: `count` (varsayılan 12) kopya, rastgele 3B dönüş + dönme, yerçekimi, dışa hız |
| mesh, çok parça (kırılma, yarımlar) | Modelin kopyası + `MeshBurst` |
| `linear: true` veri sayfası (şok halkası) | Reddedilir: "veri taşıyan sayfa, oyuna özel shader ister" (açık hata) |

Materyal: flipbook için `Project/Flipbook`; mesh için teslimin kendi (glTFast) materyali. Her şey `[SerializeField]`/asset referansı;
`VfxDefinition.lifetime` teslimden hesaplanır; aynı id tekrar üretilince asset'ler yerinde güncellenir (GUID korunur).

### 3.5 `ai_vfx_contact_sheet` (`Core/Editor/AITools/VfxContactSheetTool.cs`)
Prefab'ı geçici bir önizleme sahnesinde kurar, her an için parçacıkları `Simulate(t, withChildren, restart)` ile, `MeshBurst`'ü
`Evaluate(t)` ile o ana getirir, ortografik/perspektif kamerayla koyu ve açık iki zeminde çizer, tek PNG (`Temp/mcp_out/` varsayılan) ve
her kare için görünür piksel sayısı döndürür (boş kare = hata işareti).

## 4. Kapsam dışı

- Pota'nın kendi efekt kodunu Core'a çevirmek (Pota ayrı oturumda; bu kit yeni oyunlar ve yeni teslimler içindir).
- Şok halkası gibi ekran kırılması yapan veri sayfaları; kare karıştırma; yumuşak parçacık; VFX Graph.
- Ses (stüdyonun ses ayağı yok; Tuna bu turda istemedi).

## 5. Test

- EditMode: flipbook fikstüründen efekt (prefab'ta sayfa ızgarası, ömür, materyal shader'ı, kütüphane kaydı, ikinci üretimde GUID aynı);
  tek kare en-boy oranı; veri sayfası reddi; çok parçalı mesh'te `MeshBurst` ve parça sayısı; `MeshBurst.Evaluate` (t=0 dinlenme
  pozu, parçalar merkezden uzaklaşır, ömür sonunda ölçek 0, aynı tohum aynı sonuç); levha aracı boş olmayan kareler.
- PlayMode: `VfxService.Play` ile flipbook ve mesh efekti oynar, ömrü bitince havuza döner (parçacıksız efekt ilk karede dönmez).
- Göz: Pota teslimlerinin levhası ve vitrin kaydı; kusur "bitti" denmeden düzeltilir.
