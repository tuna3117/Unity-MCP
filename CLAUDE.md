# CLAUDE.md — Unity + MCP ile AI Destekli 3D Oyun Geliştirme

Bu dosya Unity projesinin kök klasöründe durur ve her oturumda okunur. Kullanıcıyla Türkçe konuş; kod, dosya adları ve commit mesajları İngilizce olsun.

## 1. Amaç

Bu projede sen (Claude Code) sadece C# dosyası yazan biri değilsin; bir MCP köprüsü üzerinden **çalışan Unity Editor'ü doğrudan kullanıyorsun**: sahne kuruyor, prefab üretiyor, efekt ekliyor, derleme hatalarını kendin okuyup düzeltiyor ve sonucu ekran görüntüsüyle kontrol ediyorsun. Hedef: kullanıcının tarif ettiği 3D oyunları ve görsel efektleri, kullanıcı Unity'de elle iş yapmak zorunda kalmadan üretmek.

Kullanıcı yazılımcı değil, ürünü yöneten kişi. Ona teknik karar sorma; makul varsayılanı seç, ne yaptığını kısa ve sade anlat. Sadece oyunun nasıl hissettireceğini/görüneceğini etkileyen kararları sor.

## 2. Ortam

- macOS, Apple Silicon (Metal). Unity 6, URP. Kesin sürümü `ProjectSettings/ProjectVersion.txt` dosyasından oku, ezberden varsayma.
- Editor log: `~/Library/Logs/Unity/Editor.log`
- Unity binary: `/Applications/Unity/Hub/Editor/<sürüm>/Unity.app/Contents/MacOS/Unity`
- Kurulu paketler: `Packages/manifest.json`. Bir sistemi yazmadan önce buradan kontrol et (Input System, Cinemachine, VFX Graph, ProBuilder, Test Framework var mı?).

## 3. MCP köprüsü: sıfırdan yazma, hazır olanı kur ve üstüne inşa et

Unity ↔ MCP köprüsünü sıfırdan yazmak haftalar süren ve domain reload, thread, bağlantı kopması gibi çözülmüş sorunları yeniden çözmek demek. Bunun yerine olgun bir köprü kur, **projeye özel araçları onun üstüne ekle**.

Seçenekler (ilk oturumda güncel durumlarını web'den doğrula, sonra kur):

1. **CoplayDev/unity-mcp** (MIT, ücretsiz) — varsayılan tercih. Sahne/GameObject/asset/script yönetimi, konsol okuma, test çalıştırma, menü komutu tetikleme. Python 3.10+ ve `uv` ister. Kurarken belirli bir sürüm etiketine sabitle (`#main` değil).
2. **Unity'nin resmi MCP Server'ı** (`com.unity.ai.assistant` paketi, Unity 6+) — Project Settings > AI > Unity MCP altından yönetilir. Beta ve Unity AI aboneliğine bağlı olabilir; kullanıcıya fiyat/koşul durumunu söyle, o karar versin.
3. **IvanMurzak/Unity-MCP** (Apache 2.0) — alternatif; runtime (build içi) desteği gerekirse değerlendir.

Hangi köprü kuruluysa **araç adlarını ezberden yazma**; oturum başında mevcut MCP araçlarını listele ve ona göre çalış. Köprüye özel notları bu dosyanın sonundaki "Proje durumu" bölümüne yaz.

**Köprü yokken/çalışmıyorken:** dosya sistemi + `Editor.log` okuma ile devam et. Editor kapalıysa batch mode kullanılabilir:
`Unity -batchmode -projectPath . -executeMethod Sinif.Metod -quit -logFile -`
(Proje Editor'de açıkken batch mode çalışmaz; aynı projeyi iki Unity açamaz.)

## 4. Çalışma döngüsü (her değişiklikte)

1. **Oku** — dokunacağın sistemle ilgili mevcut scriptleri, sahne hiyerarşisini (MCP ile) ve paketleri incele. Mevcut mimariye uy.
2. **Küçük adım at** — tek seferde tek sistem. 10 dosyayı birden yazıp sonra derlemeye çalışma.
3. **Derlet ve bekle** — script değişince Unity yeniden derler ve *domain reload* yapar; bu sırada MCP bağlantısı birkaç saniye kopabilir. Bu bir hata değil: bekle, yeniden dene. Derleme bitmeden sahneye o scripti component olarak eklemeye çalışma.
4. **Konsolu oku** — hata ve uyarıları MCP konsol aracıyla (yoksa `Editor.log`) oku. Hata varsa kullanıcıya sormadan düzelt, tekrar derlet. Aynı hatada 3 denemede çözemezsen dur, durumu anlat.
5. **Gözle doğrula** — sahne, efekt, UI gibi görsel işlerde ekran görüntüsü al (Scene/Game view) ve gerçekten baktığında doğru görünüyor mu kontrol et. "Kod derlendi" ≠ "doğru görünüyor". Gerekirse Play Mode'a gir, görüntü al, çık.
6. **Commit** — çalışan her anlamlı adımdan sonra git commit. Toplu/yıkıcı işlemlerden (toplu yeniden adlandırma, materyal değiştirme, asset taşıma) **önce** de commit at ki geri dönülebilsin.

## 5. Unity'de asla yapma / dikkat et

- **`.meta` dosyaları:** Asset'leri `mv`/`rm`/`cp` ile taşıma, silme, kopyalama. GUID referansları kırılır, sahnelerdeki bağlantılar sessizce kaybolur. Taşıma/yeniden adlandırma/silme işlemlerini MCP asset araçlarıyla veya `AssetDatabase` API'siyle yap. Yeni `.cs` dosyasını doğrudan yazabilirsin; `.meta`'sını Unity üretir, sen üretme.
- **`.unity`, `.prefab`, `.asset`, `.mat` dosyalarını elle (YAML olarak) düzenleme.** Sahne ve prefab değişikliklerini MCP araçlarıyla veya Editor script'iyle yap.
- **`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` klasörlerine dokunma**; bunlar git'e de girmez (Unity `.gitignore` yoksa ekle).
- **Play Mode'da yapılan sahne değişiklikleri çıkınca silinir.** Kalıcı değişiklikleri Edit Mode'da yap.
- **Referansları Inspector üzerinden bağla** (`[SerializeField] private`), `GameObject.Find("isim")` ile string'e bağlı kod yazma. Referans atamasını MCP ile yap ve atandığını doğrula — "NullReferenceException"ların çoğu atanmamış referanstır.
- `Packages/manifest.json` ve `ProjectSettings/` değişikliklerini yapabilirsin ama ne değiştirdiğini kullanıcıya söyle.
- Kullanıcıya sormadan asset silme, paket kaldırma, sahne üzerine yazma yapma.

## 6. Unity 6 / URP — eğitim verindeki eski bilgilere güvenme

Unity hakkındaki bilgilerinin çoğu eski sürümlerden geliyor. Emin olmadığın API'de kurulu paketin kaynak kodunu (`Library/PackageCache/`) veya güncel dokümantasyonu kontrol et. Sık yapılan hatalar:

- `FindObjectOfType` → `FindFirstObjectByType` / `FindAnyObjectByType`
- `Rigidbody.velocity` → `linearVelocity`; `drag` → `linearDamping`; `angularDrag` → `angularDamping`
- **Input:** Yeni Input System kuruluysa `UnityEngine.Input.GetKey/GetAxis` çalışmaz (exception atar). `manifest.json` ve Player Settings'teki Active Input Handling'e bak; Input Actions asset'i üzerinden git.
- **URP özel render pass / renderer feature:** Unity 6'da Render Graph API kullanılır (`RecordRenderGraph`). Eski `Execute(ScriptableRenderContext, ...)` örnekleri çalışmaz ya da uyumluluk moduna düşer. İnternetteki 2021–2022 URP örneklerini kopyalama.
- **Shader:** Built-in pipeline shader'ları (`CGPROGRAM`, `UnityCG.cginc`, surface shader) URP'de pembe görünür. URP için `HLSLPROGRAM` + URP `ShaderLibrary` include'ları, `UniversalForward` pass tag'i ve SRP Batcher uyumlu `CBUFFER_START(UnityPerMaterial)` kullan.
- Pembe materyal = shader pipeline uyumsuzluğu. Önce bunu düşün.
- Cinemachine 3.x API'si 2.x'ten farklıdır (`CinemachineCamera`, `CinemachineVirtualCamera` değil); kurulu sürüme bak.

## 7. Kod mimarisi

- Klasör yapısı: `Assets/_Project/{Scripts, Editor, Prefabs, Materials, Shaders, VFX, Scenes, Settings, Art, Audio, Tests}`. Üçüncü parti asset'ler `_Project` dışında kalır.
- `Assets/_Project/Scripts` ve `Editor` için **assembly definition (.asmdef)** kullan: derleme süresi kısalır, bu da senin derle-kontrol et döngünü hızlandırır.
- Küçük, tek işi olan MonoBehaviour'lar. Ayarlanabilir veriler (hız, hasar, dalga tanımı, efekt parametreleri) **ScriptableObject**'te dursun ki kullanıcı kod görmeden Inspector'dan ayar yapabilsin.
- Sistemler arası iletişimde C# event / ScriptableObject event kanalı; her şeyin her şeye eriştiği singleton ağı kurma. `GameManager` gibi birkaç gerçek global için basit singleton kabul.
- Sık üretilen nesneler (mermi, efekt, düşman) için `UnityEngine.Pool.ObjectPool` kullan.
- Oyun mantığı için EditMode/PlayMode testleri yaz (Unity Test Framework) ve MCP üzerinden çalıştır; bu sana görsel olmayan işlerde otomatik doğrulama sağlar.
- Aşırı mühendislik yapma: prototipte DI framework, karmaşık katmanlar gereksiz. Önce çalışsın ve oynanabilsin.

## 8. Projeye özel AI araç katmanı (`Assets/_Project/Editor/AITools/`)

Köprünün genel araçları tek tek obje işlemleri için iyidir; tekrarlayan veya toplu işler için **kendi Editor araçlarını yaz** ve onları çağır. Bir işi ikinci kez elle (çok sayıda tekil MCP çağrısıyla) yapıyorsan, araç haline getir.

- Her araç `static` metot + `[MenuItem("AI Tools/...")]` olsun; böylece hem köprünün "menü komutu çalıştır" aracıyla hem batch mode `-executeMethod` ile tetiklenebilir. Köprü özel araç kaydını destekliyorsa (paketin kendi dokümantasyonunu oku, API'yi tahmin etme) parametreli araçları oradan da kaydet.
- Tüm sahne değişikliklerinde `Undo.RecordObject` / `Undo.RegisterCreatedObjectUndo` kullan; iş bitince sahneyi dirty işaretle ve kaydet.
- Araç ne yaptığını `Debug.Log` ile özetlesin ("37 obje güncellendi") ki konsoldan sonucu okuyabilesin.
- Başlangıç seti: toplu yeniden adlandırma, toplu materyal atama, seçimden prefab üretme, objeleri yüzeye/grid'e dağıtma (scatter), sahne ekran görüntüsü alma (belirli kamera açılarından), eksik referans/eksik script taraması, URP'ye uyumsuz (pembe) materyal taraması.

## 9. Görsel efektler (VFX) — neyi iyi yaparsın, neyi yapamazsın

**Doğrudan üretebildiklerin (tercih et):**
- **Particle System (Shuriken):** tüm modülleri koddan/Editor script'inden ayarlanabilir. Patlama, duman, kıvılcım, toz, iz, büyü efekti, hit efekti için ana aracın bu. Her efekti `Assets/_Project/VFX/` altında prefab yap.
- **Elle yazılmış URP HLSL shader'ları:** dissolve, fresnel/rim, hologram, kalkan, su, toon, outline, UV scroll, vertex animasyonu (rüzgâr, dalga).
- **Post-processing:** Global/Local Volume + Volume Profile (Bloom, Color Adjustments, Vignette, Chromatic Aberration, Depth of Field, Motion Blur). Parlayan efektler için HDR emission + Bloom birlikte çalışır; ikisini de ayarla.
- **"Juice":** kamera sarsıntısı (Cinemachine Impulse), hit-stop (kısa `timeScale` düşüşü), squash & stretch, vuruşta beyaz flaş (MaterialPropertyBlock), Trail/Line Renderer, decal'ler, ışık flaşı, ekran efektleri (full screen pass renderer feature).
- Tween gerekiyorsa küçük bir yardımcı yaz ya da kullanıcı onaylarsa bir tween kütüphanesi ekle.

**Zayıf olduğun yerler (dürüst ol):**
- **VFX Graph ve Shader Graph** dosyaları görsel düğüm grafiğidir; metin olarak güvenilir biçimde yazılamaz. Bunları sıfırdan üretmeye çalışma. Gerekirse: hazır/şablon bir graph'ın *exposed parametrelerini* koddan sür ya da aynı efekti Particle System + HLSL ile yap.
- **3D model, texture, animasyon, ses üretemezsin.** Blokaj için primitive'ler ve ProBuilder kullan; gerçek asset için kullanıcıya ücretsiz/CC0 kaynak öner (Kenney, Quaternius, Poly Haven, Mixamo, Unity Asset Store ücretsizleri) ve içe aktarma ayarlarını (scale, materyal dönüştürme, rig tipi) sen yap. Basit prosedürel texture'ları (noise, gradient) koddan üretebilirsin.

**Her efekt için standart:** prefab + parametreleri tutan ScriptableObject + tek satırla çağrılabilen bir API (`VfxService.Play(vfxId, position, rotation)`) + pool. Efekti bitirince Play Mode'da tetikleyip ekran görüntüsüyle kontrol et. Mobil hedef varsa parçacık sayısı ve overdraw'a dikkat et.

## 10. Oyun üretim akışı

Kullanıcı bir oyun fikri verdiğinde:
1. Fikri 5–8 maddelik mini tasarıma çevir (çekirdek döngü, kontroller, kamera, kazanma/kaybetme, hedef platform). Eksik kritik bilgiyi tek seferde sor.
2. **Önce gri kutu:** primitive'lerle oynanabilir çekirdek döngüyü kur. Görsellik sonra.
3. Oynanış oturunca: asset'ler → ışık ve post-processing → efektler ve juice → UI → ses → menü/akış.
4. Her aşama sonunda kullanıcıya: ne çalışıyor, Unity'de nasıl dener (hangi sahne, Play'e bas, hangi tuşlar), sıradaki adım ne.

## 11. İlk oturum kurulum listesi

Aşağıdakileri sırayla yap, bitenleri işaretle ve "Proje durumu"nu güncelle:

- [x] `ProjectVersion.txt` ve `manifest.json` oku; Unity sürümü, URP sürümü, input sistemi durumunu not et
- [x] Git yoksa başlat, Unity `.gitignore` ekle, ilk commit
- [x] MCP köprüsünü seç (bkz. §3), güncel kurulum adımlarını resmi dokümanından doğrula, kur, Claude Code'a proje kapsamında kaydet (`.mcp.json`)
- [x] Duman testi: sahne hiyerarşisini oku → bir küp oluştur → materyal ata → ekran görüntüsü al → bilerek hatalı bir script yaz, konsoldan hatayı oku, düzelt → küpü ve scripti sil
- [x] Klasör yapısını ve asmdef'leri oluştur (§7)
- [x] `AITools` başlangıç setini yaz ve her birini test et (§8)
- [x] `VfxService` + pool + örnek 3 efekt (hit, patlama, toplama/pickup) ve bir post-processing Volume kur (§9)
- [ ] Kullanıcıya kısa rapor ver ve ilk oyun fikrini iste

## 12. Proje durumu (bunu sen güncel tut)

_Son güncelleme: 2026-09-19 (basket turu yeniden yapımı)._

- **Unity sürümü:** 6000.6.2f1 (Unity 6.6), macOS 26.6 / Apple Silicon / Metal. Proje `urp-blank` şablonundan üretildi.
- **URP sürümü:** 17.6.0. Render Graph aktif; `Assets/Settings/` altında PC/Mobile RP asset'leri şablondan geliyor.
- **Input:** Yeni Input System 1.20.0 (`Assets/InputSystem_Actions.inputactions` şablondan). Eski `UnityEngine.Input` kullanma.
- **Kurulu olmayanlar:** Cinemachine, ProBuilder, VFX Graph, TextMeshPro (gerekince `manage_packages` ile kur ve buraya yaz).
- **MCP köprüsü ve sürümü:** CoplayDev/unity-mcp **v10.2.0**, `Packages/com.coplaydev.unity-mcp` altına **gömülü (embedded)** ve yamalı. Python sunucusu `mcpforunityserver` (uvx ile), HTTP transport, `http://localhost:8080/mcp`. Proje kapsamlı `.mcp.json` var; paketin kurulum sihirbazı ayrıca `~/.claude.json` içine aynı URL ile local-scope `UnityMCP` kaydı yazdı (ikisi aynı sunucuya gider, biri fazla ama zararsız).
- **Unity 6.6 yaması (önemli):** `Packages/com.coplaydev.unity-mcp/Runtime/Helpers/UnityObjectIdCompat.cs` → `InstanceIDToObjectCompat`. Unity 6000.6'da paketin yansımayla çağırdığı `EditorUtility.InstanceIDToObject(int)` `NotImplementedException` atıyor; yama, int handle'ı EntityId'nin versiyon kelimesini (`0x100 | v<<9`, v=0..255) deneyerek tam EntityId'ye çeviriyor, bulamazsa tüm objeleri tarıyor. Bu olmadan hedef obje alan **hiçbir** araç (materyal atama, bileşen, silme...) çalışmaz. Paketi güncellerken bu yamayı koru (upstream'e PR açılabilir).
- **Köprüye özel notlar:**
  - `Assets/_Project/Editor/AITools/McpBridgeBootstrap.cs` her editor açılışında/domain reload'da yerel HTTP sunucuyu başlatır ve köprüyü bağlar (paketin "Auto-Start on Editor Load" ayarı da açık). Menü: `AI Tools/MCP/Ensure Bridge Running`, `AI Tools/MCP/Log Status`.
  - Domain reload (script değişikliği, Play Mode giriş/çıkış) sonrası köprü ~5 sn kopar; yeniden dene.
  - Shell'den araç çağırma (proje kökünden): `uv run Tools/mcp_call.py list` / `describe <tool>` / `call <tool> '<json>'` / `call <tool> @dosya.json`. Görüntüler `Temp/mcp_out/` altına iner.
  - Projeye özel araçlar: `execute_custom_tool {"tool_name":"ai_...","parameters":{...}}`. Yeni `[McpForUnityTool]` sınıfı `Assets/_Project/Editor/` altına yazılır; derleme sonrası otomatik kaydolur.
  - Script değişikliği sonrası derletme: `refresh_unity {"mode":"force","scope":"all","compile":"request","wait_for_ready":true}` → sonra `read_console {"types":["error"],"filter_text":"error CS"}`.
  - Unity'nin kendi asset yenilemesi yalnızca Editor odak alınca çalışır; köprü yoksa `open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app` ile odak ver.
  - Ekran görüntüsü: `manage_camera {"action":"screenshot","capture_source":"game_view"|"scene_view","view_target":...,"view_position":[x,y,z],"include_image":true}`. Play Mode'da efekt yakalamak için `EditorApplication.update` ile tekrarlı spawner kur ve ana kamerayı yaklaştır (Play Mode değişiklikleri kalıcı değil); "duraklat-çek" yaklaşımı efekti yakalayamadı.
  - `manage_material create` `color` parametresini uygulamıyor → ardından `set_material_color` çağır. Bilinmeyen shader adı sessizce URP/Lit'e düşer.
  - `execute_code` CodeDom (C# 6) ile derler: tuple/`out var` yok, obsolete API (`GetInstanceID`) derlemeyi bozar. Proje tiplerine `System.Type.GetType("Project.VFX.VfxService, Project.Runtime")` ile eriş.
  - `apply_text_edits` için önce `get_sha` ile `precondition_sha256` al.
  - Konsolda tekrar eden `currentFileSystemTime.ticks != 0 ... FSTimeGet` hatası Unity/macOS kaynaklı gürültü; `.meta` dosyalarına 2262 tarihli mtime yazıyor, işlevsel etkisi görülmedi.
  - `defaults write` ile dışarıdan yazılan EditorPrefs'i çalışan Unity görmüyor; ayarları Unity içinden (script/menü) set et.
- **Yazılmış AITools** (`Assets/_Project/Editor/AITools/`, hem menü hem MCP özel aracı, hepsi test edildi; basket araçları aşağıda ayrıca):
  - `ai_bulk_rename` — desenli toplu yeniden adlandırma (`{name}`, `{i}`, `{i:00}`)
  - `ai_bulk_assign_material` — çok objeye materyal atama (slot seçimi, alt objeler)
  - `ai_create_prefab` — sahne objesinden prefab üret ve bağla
  - `ai_scatter` — prefab'ı yüzeye/grid'e dağıt (raycast, rastgele dönüş/ölçek, seed)
  - `ai_scan_missing` — eksik script + kopuk referans taraması (`fix_missing_scripts`)
  - `ai_scan_pink_materials` — URP uyumsuz materyal taraması (`fix` = MaterialUpgrader ile dönüştür)
  - `ai_vfx_create_starters` — başlangıç efekt setini üretir
  - Ekran görüntüsü için köprünün `manage_camera` aracı yeterli olduğundan ayrı araç yazılmadı.
- **Hazır efektler** (`Assets/_Project/VFX/`, `VfxService.Play("hit"|"explosion"|"pickup", pos, rot)`): prefab + `VfxDefinition` + `VfxLibrary.asset`, `UnityEngine.Pool` ile havuzlanır, sahnede `VfxService` objesi var. Global Volume: Bloom 1.0 / eşik 0.9, Vignette 0.25, Color Adjustments (kontrast +10, doygunluk +8). Yeni efekt: prefab + `Project/VFX/VFX Definition` asset'i + kütüphaneye ekle.
- **Testler:** EditMode `Assets/_Project/Tests/EditMode` (`run_tests {"mode":"EditMode"}` → `get_test_job`).
- **Basket turu (Pota "Faz A" yeniden yapımı, 2026-09-19):** sahne `Assets/_Project/Scenes/Basket.unity`, kod `Assets/_Project/Scripts/Basket/` (saf kurallar: `AimMapper`, `TrajectorySolver`, `LevelRules`, `RoundRules`; bileşenler: `BasketRound` durum makinesi, `Ball`/`BallPool`, `Hoop`, `BasketCage`, `HoopColumnBuilder`, `RoundCamera`, `BasketJuice`, `BasketHud`, `Visual/*`), ayarlar `Assets/_Project/Settings/BasketSettings.asset` (tüm sayılar orada; koda gömme). Tasarım `docs/superpowers/specs/2026-09-18-basket-round-design.md`, plan `docs/superpowers/plans/2026-09-18-basket-round.md`, ölçüm `docs/measurements/2026-09-19-basket-sweep-L1.md`, görüntüler `docs/screenshots/`.
  - Araçlar: `ai_basket_build_scene` (sahneyi sıfırdan kurar; katmanlar BallFlight/BallDrop/Hoop/Slab, fizik malzemeleri, sanat referansları), `ai_basket_sweep` (Play Mode'da atış taraması, action start/status/cancel), `ai_basket_ball_texture` (basketbol dokusu). Menü: `AI Tools/Basket/...`.
  - Kaynak oyun (three.js) yerel bir klasörde, salt okunur; görselleri `Assets/_Project/Art/Pota/` altına kopyalandı (`oyuncu.glb` glTFast ile).
  - Fidelite notları: uçuş 1 sn parabol, 0.09 sn top aralığı, ×2/+1 kuralları, bölüm tablosu, sepet sayımı orijinalle aynı. 3B'ye özel devir: uçan top pota katmanıyla çarpışmaz, z=0'da Drop fazına geçer (ileri hız ×0.1); 0.45 m üstünden gelen top yanal hız ×0.3 ("panoya çarptı"), 1 m üstünden gelen yana itilir ("panoyu aştı"). Geçiş: yukarıdan giren top düzlemin altında R−r/2 içindeyse sayılır. 1. bölüm taraması: min 12 / medyan 19 / maks 28 (orijinal 12 / 21 / 32).
  - Play Mode içinde script derleme YAPMA: domain reload havuzu/abonelikleri bozuyor ve konsolun hata-duraklatması editörü "Pause"a alabiliyor (`EditorApplication.isPaused`). Önce `manage_editor stop`.
  - Denemek için: Basket sahnesini aç, Play, fareyle aşağıdan yukarı sürükle (sola/sağa pota seçer, uzunluk güç); "Tekrar" düğmesi turu sıfırlar. Bölüm değiştirmek: `Round` objesindeki `Level` alanı.
  - Sıradaki adaylar: üst kuyruk ayarı (alt sıra geçişleri / CloneScatter), telefon dokunmatik testi ve build, Faz B (bulmaca) entegrasyonu.
- **Aktif oyun / mevcut aşama:** Basket turu 4 aşamada bitti (gri kutu → kamera/his → görsel → ölçüm). Kullanıcının his geri bildirimi bekleniyor.
