# CLAUDE.md — Unity + MCP ile AI Destekli 3D Oyun Geliştirme

Bu dosya Unity projesinin kök klasöründe durur ve her oturumda okunur. Kullanıcıyla Türkçe konuş; kod, dosya adları ve commit mesajları İngilizce olsun.

## 1. Amaç

Bu projede sen (Claude Code) sadece C# dosyası yazan biri değilsin; bir MCP köprüsü üzerinden **çalışan Unity Editor'ü doğrudan kullanıyorsun**: sahne kuruyor, prefab üretiyor, efekt ekliyor, derleme hatalarını kendin okuyup düzeltiyor ve sonucu ekran görüntüsüyle kontrol ediyorsun. Hedef: kullanıcının tarif ettiği 3D oyunları ve görsel efektleri, kullanıcı Unity'de elle iş yapmak zorunda kalmadan üretmek.

Kullanıcı yazılımcı değil, ürünü yöneten kişi. Ona teknik karar sorma; makul varsayılanı seç, ne yaptığını kısa ve sade anlat. Sadece oyunun nasıl hissettireceğini/görüneceğini etkileyen kararları sor.

**Bu projenin rolü: atölye + şablon.** Ortak araçlar (köprü yaması, AI araçları, efekt seti, juice bileşenleri, karakter hattı) burada gelişir, yeni oyun fikirleri ilk denemelerini burada yapar. Yayına gidecek her oyun kendi Unity projesinde yaşar, çünkü bir Unity projesinden tek bir uygulama çıkar (tek ikon, bundle id, build). Örnek: `~/projects/Kaptan` bu projeden kopyalandı. Yeni oyun projesi açarken bu projeyi kopyala, `Assets/_Project/Experiments/` ile o oyuna ait olmayan `Games/*` klasörlerini sil (bkz. §7).

**Stüdyo haritası** (copy → Blender → Unity/three.js, teslim sözleşmesi, yol haritası): `~/projects/Blender MCP/docs/studyo.md`. 3B model/iskelet/animasyon/efekt dokusu Blender atölyesinde (`~/projects/Blender MCP`) yapılır, buraya `ai_import_asset` ile gelir. Ertelenen küçük işler (Unity tarafı dâhil; açık olanlar B1–B9, U1–U5, U11–U28): `~/projects/Blender MCP/docs/backlog.md`; Tuna en sonunda hepsinin düzeltilmesini bekliyor.

## 2. Ortam

- macOS, Apple Silicon (Metal). Unity 6, URP. Kesin sürümü `ProjectSettings/ProjectVersion.txt` dosyasından oku, ezberden varsayma.
- Editor log: Unity 6.6 açılıştan sonra proje içindeki `Logs/Editor.log`'a yazar (`~/Library/Logs/Unity/Editor.log` yalnızca açılışın ilk satırlarını tutar).
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

- `FindObjectOfType` → `FindAnyObjectByType` (Unity 6.6'da `FindFirstObjectByType` de obsolete uyarısı veriyor)
- `Rigidbody.velocity` → `linearVelocity`; `drag` → `linearDamping`; `angularDrag` → `angularDamping`
- **Input:** Yeni Input System kuruluysa `UnityEngine.Input.GetKey/GetAxis` çalışmaz (exception atar). `manifest.json` ve Player Settings'teki Active Input Handling'e bak; Input Actions asset'i üzerinden git.
- **URP özel render pass / renderer feature:** Unity 6'da Render Graph API kullanılır (`RecordRenderGraph`). Eski `Execute(ScriptableRenderContext, ...)` örnekleri çalışmaz ya da uyumluluk moduna düşer. İnternetteki 2021–2022 URP örneklerini kopyalama.
- **Shader:** Built-in pipeline shader'ları (`CGPROGRAM`, `UnityCG.cginc`, surface shader) URP'de pembe görünür. URP için `HLSLPROGRAM` + URP `ShaderLibrary` include'ları, `UniversalForward` pass tag'i ve SRP Batcher uyumlu `CBUFFER_START(UnityPerMaterial)` kullan.
- Pembe materyal = shader pipeline uyumsuzluğu. Önce bunu düşün.
- Cinemachine 3.x API'si 2.x'ten farklıdır (`CinemachineCamera`, `CinemachineVirtualCamera` değil); kurulu sürüme bak.

## 7. Kod mimarisi

- Klasör yapısı **oyuna göre**, dosya türüne göre değil. Üçüncü parti asset'ler `_Project` dışında kalır.
  ```
  Assets/_Project/
    Core/            her oyunun kullandığı ortak parçalar
      Scripts/       Project.Runtime  (VFX: VfxService/havuz, Juice: CameraShake, TimeScaleController, PopupText, SynthSfx,
                                       Dev: DevSceneSwitcher)
      Editor/        Project.Editor   (GameProfile, GameViewAutoResolution; AITools/: genel AI araçları, köprü açılışı)
      VFX/           hazır efekt seti + VfxLibrary.asset
      Shaders/       Toon.shader (`Project/Toon`: iki tonlu gölge, kenar parlaması, dış hat, gölge alır/verir)
      Tests/         EditMode (Project.Tests.EditMode), PlayMode (Project.Tests.PlayMode: sahne açılış testi)
    Games/<Oyun>/    bir oyunun her şeyi + README.md (oyunun notları) + <Oyun>Profile.asset (GameProfile)
      Scripts/ Editor/ Tests/{EditMode,PlayMode}   <Oyun>.Runtime / .Editor / .Tests.* asmdef'leri
      Scenes/ Settings/ Art/ Prefabs/ Anim/ Shaders/ Materials/ Audio/ (gerektikçe)
    Experiments/     geçici denemeler, oyuna bağlanmayan işler
  ```
- **Bağımlılık yönü:** oyun Core'u kullanır, Core hiçbir oyunu kullanmaz (asmdef'ler bunu derleme hatasıyla zorlar). Oyuna özel Editor aracı `Games/<Oyun>/Editor/`e yazılır. Bir oyunun kodu ikinci bir oyuna lazım olunca Core'a taşı (juice bileşenleri böyle taşındı).
- Yeni oyun: `Games/<Oyun>/` aç, Pota'nın dört asmdef'ini örnek al (ad ve referansları değiştir), `README.md` yaz, bir `GameProfile` oluştur (`Games/<Oyun>/<Oyun>Profile.asset`: telefon çözünürlüğü, ürün adı, bundle id, Apple takımı, sahneler). Profil olunca oyunun sahnesi açılınca Game view o çözünürlüğe geçer (`GameViewAutoResolution`) ve `ai_build` o oyunu derleyebilir. Araçlara klasör yolu verirken hep oyunun klasörünü ver; genel araçların varsayılan klasörü `Experiments/`.
- Asset taşıma/yeniden adlandırma: `ai_move_assets` (AssetDatabase, GUID korunur, önce `dry_run`). Bir oyunu başka Unity projesine götürmek: `Games/<Oyun>/` klasörünü `.meta` dosyalarıyla birlikte kopyala (GUID'ler korunur); hedefte `Core/` da olmalı.
- **Assembly definition (.asmdef)** kullan: derleme süresi kısalır, bu da senin derle-kontrol et döngünü hızlandırır.
- Küçük, tek işi olan MonoBehaviour'lar. Ayarlanabilir veriler (hız, hasar, dalga tanımı, efekt parametreleri) **ScriptableObject**'te dursun ki kullanıcı kod görmeden Inspector'dan ayar yapabilsin.
- Sistemler arası iletişimde C# event / ScriptableObject event kanalı; her şeyin her şeye eriştiği singleton ağı kurma. `GameManager` gibi birkaç gerçek global için basit singleton kabul.
- Sık üretilen nesneler (mermi, efekt, düşman) için `UnityEngine.Pool.ObjectPool` kullan.
- Oyun mantığı için EditMode/PlayMode testleri yaz (Unity Test Framework) ve MCP üzerinden çalıştır; bu sana görsel olmayan işlerde otomatik doğrulama sağlar.
- Aşırı mühendislik yapma: prototipte DI framework, karmaşık katmanlar gereksiz. Önce çalışsın ve oynanabilsin.

## 8. Projeye özel AI araç katmanı (`Assets/_Project/Core/Editor/AITools/`, oyuna özel olanlar `Games/<Oyun>/Editor/`)

Köprünün genel araçları tek tek obje işlemleri için iyidir; tekrarlayan veya toplu işler için **kendi Editor araçlarını yaz** ve onları çağır. Bir işi ikinci kez elle (çok sayıda tekil MCP çağrısıyla) yapıyorsan, araç haline getir.

- Her araç `static` metot + `[MenuItem("AI Tools/...")]` olsun; böylece hem köprünün "menü komutu çalıştır" aracıyla hem batch mode `-executeMethod` ile tetiklenebilir. Köprü özel araç kaydını destekliyorsa (paketin kendi dokümantasyonunu oku, API'yi tahmin etme) parametreli araçları oradan da kaydet.
- Tüm sahne değişikliklerinde `Undo.RecordObject` / `Undo.RegisterCreatedObjectUndo` kullan; iş bitince sahneyi dirty işaretle ve kaydet.
- Araç ne yaptığını `Debug.Log` ile özetlesin ("37 obje güncellendi") ki konsoldan sonucu okuyabilesin.
- Başlangıç seti: toplu yeniden adlandırma, toplu materyal atama, seçimden prefab üretme, objeleri yüzeye/grid'e dağıtma (scatter), sahne ekran görüntüsü alma (belirli kamera açılarından), eksik referans/eksik script taraması, URP'ye uyumsuz (pembe) materyal taraması.

## 9. Görsel efektler (VFX) — neyi iyi yaparsın, neyi yapamazsın

**Doğrudan üretebildiklerin (tercih et):**
- **Particle System (Shuriken):** tüm modülleri koddan/Editor script'inden ayarlanabilir. Patlama, duman, kıvılcım, toz, iz, büyü efekti, hit efekti için ana aracın bu. Her efekti prefab yap: her oyunun kullanabileceği efektler `Assets/_Project/Core/VFX/`, oyuna özel olanlar `Games/<Oyun>/VFX/`.
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

_Son güncelleme: 2026-09-30 (Renk Avı Pota'nın bulmaca görünümüyle birebir: bloklar, etiketler, patlama katmanları, salvo, dönüş, HUD, sesler, Pota çatısı; Pota | Unity karşılaştırma araçları; sıradaki: "üstüne inşa" adayları, ayrı spec)._

- **Unity sürümü:** 6000.6.2f1 (Unity 6.6), macOS 26.6 / Apple Silicon / Metal. Proje `urp-blank` şablonundan üretildi.
- **URP sürümü:** 17.6.0. Render Graph aktif; `Assets/Settings/` altında PC/Mobile RP asset'leri şablondan geliyor.
- **Input:** Yeni Input System 1.20.0 (`Assets/InputSystem_Actions.inputactions` şablondan). Eski `UnityEngine.Input` kullanma.
- **Kurulu olmayanlar:** Cinemachine, ProBuilder, VFX Graph, TextMeshPro (gerekince `manage_packages` ile kur ve buraya yaz).
- **MCP köprüsü ve sürümü:** CoplayDev/unity-mcp **v10.2.0**, `Packages/com.coplaydev.unity-mcp` altına **gömülü (embedded)** ve yamalı. Python sunucusu `mcpforunityserver` (uvx ile), HTTP transport, `http://localhost:8080/mcp`. Proje kapsamlı `.mcp.json` var; paketin kurulum sihirbazı ayrıca `~/.claude.json` içine aynı URL ile local-scope `UnityMCP` kaydı yazdı (ikisi aynı sunucuya gider, biri fazla ama zararsız).
- **Unity 6.6 yaması (önemli):** `Packages/com.coplaydev.unity-mcp/Runtime/Helpers/UnityObjectIdCompat.cs` → `InstanceIDToObjectCompat`. Unity 6000.6'da paketin yansımayla çağırdığı `EditorUtility.InstanceIDToObject(int)` `NotImplementedException` atıyor; yama, int handle'ı EntityId'nin versiyon kelimesini (`0x100 | v<<9`, v=0..255) deneyerek tam EntityId'ye çeviriyor, bulamazsa tüm objeleri tarıyor. Bu olmadan hedef obje alan **hiçbir** araç (materyal atama, bileşen, silme...) çalışmaz. Paketi güncellerken bu yamayı koru (upstream'e PR açılabilir).
- **Köprüye özel notlar:**
  - `Assets/_Project/Core/Editor/AITools/McpBridgeBootstrap.cs` her editor açılışında/domain reload'da yerel HTTP sunucuyu başlatır ve köprüyü bağlar (paketin "Auto-Start on Editor Load" ayarı da açık). Menü: `AI Tools/MCP/Ensure Bridge Running`, `AI Tools/MCP/Log Status`.
  - Domain reload (script değişikliği, Play Mode giriş/çıkış) sonrası köprü ~5 sn kopar; yeniden dene.
  - **Oturum başı:** `Tools/unity_up.sh` — proje Unity'de açık değilse açar (başka proje açıksa ayrı Unity başlatır) ve köprü bu projeyi görene kadar bekler.
  - Shell'den araç çağırma (proje kökünden): `~/.local/bin/uv run Tools/mcp_call.py list` / `describe <tool>` / `call <tool> '<json>'` / `call <tool> @dosya.json` / `read <uri>` (ör. `mcpforunity://instances`, `mcpforunity://editor/state`). `uv` Claude'un kabuğunda PATH'te değil, tam yolla çağır. Görüntüler `Temp/mcp_out/` altına iner.
  - Projeye özel araçlar: `execute_custom_tool {"tool_name":"ai_...","parameters":{...}}`. Yeni `[McpForUnityTool]` sınıfı `Core/Editor/AITools/` (genel) ya da `Games/<Oyun>/Editor/` (oyuna özel) altına yazılır; derleme sonrası otomatik kaydolur (köprü `TypeCache` ile tüm assembly'leri tarar).
  - Script değişikliği sonrası derletme: `refresh_unity {"mode":"force","scope":"all","compile":"request","wait_for_ready":true}` → sonra `read_console {"types":["error"],"filter_text":"error CS"}`.
  - Unity'nin kendi asset yenilemesi yalnızca Editor odak alınca çalışır; köprü yoksa `open -a /Applications/Unity/Hub/Editor/6000.6.2f1/Unity.app` ile odak ver.
  - Ekran görüntüsü: `manage_camera {"action":"screenshot","capture_source":"game_view"|"scene_view","view_target":...,"view_position":[x,y,z],"include_image":true}`. Play Mode'da efekt yakalamak için `EditorApplication.update` ile tekrarlı spawner kur ve ana kamerayı yaklaştır (Play Mode değişiklikleri kalıcı değil); "duraklat-çek" yaklaşımı efekti yakalayamadı.
  - `manage_material create` `color` parametresini uygulamıyor → ardından `set_material_color` çağır. Bilinmeyen shader adı sessizce URP/Lit'e düşer.
  - `execute_code` CodeDom (C# 6) ile derler: tuple/`out var` yok, obsolete API (`GetInstanceID`) derlemeyi bozar. Proje tiplerine `System.Type.GetType("Project.VFX.VfxService, Project.Runtime")` ile eriş (oyun tipleri kendi assembly'sinde, ör. `"Project.Basket.BasketRound, Pota.Runtime"`).
  - `apply_text_edits` için önce `get_sha` ile `precondition_sha256` al.
  - Konsolda tekrar eden `currentFileSystemTime.ticks != 0 ... FSTimeGet` hatası Unity/macOS kaynaklı gürültü; `.meta` dosyalarına 2262 tarihli mtime yazıyor, işlevsel etkisi görülmedi.
  - `defaults write` ile dışarıdan yazılan EditorPrefs'i çalışan Unity görmüyor; ayarları Unity içinden (script/menü) set et.
- **Yazılmış AITools** (Core, `Assets/_Project/Core/Editor/AITools/`, hem menü hem MCP özel aracı, hepsi test edildi):
  - `ai_bulk_rename` — desenli toplu yeniden adlandırma (`{name}`, `{i}`, `{i:00}`)
  - `ai_bulk_assign_material` — çok objeye materyal atama (slot seçimi, alt objeler)
  - `ai_create_prefab` — sahne objesinden prefab üret ve bağla (varsayılan klasör `Experiments/Prefabs`; oyunun klasörünü ver)
  - `ai_scatter` — prefab'ı yüzeye/grid'e dağıt (raycast, rastgele dönüş/ölçek, seed)
  - `ai_scan_missing` — eksik script + kopuk referans taraması (`fix_missing_scripts`; `scope: prefabs` tüm `_Project` prefab'larını tarar)
  - `ai_scan_pink_materials` — URP uyumsuz materyal taraması (`fix` = MaterialUpgrader ile dönüştür)
  - `ai_vfx_create_starters` — başlangıç efekt setini üretir
  - `ai_anim_contact_sheet` — model + klip → tek PNG + kemik yükseklik tablosu. Deri her karede CPU'da pişirilip çizilir (eskiden yalnız kök kemiğin hareketi görünüyordu: şutta kollar kalçada kalıyordu), kadraj bütün pozları kapsar
  - `ai_move_assets` — asset/klasör taşıma ve yeniden adlandırma (GUID korunur; `dry_run`, boş klasör temizliği, Build Settings sahne yollarını eşitler)
  - `ai_import_asset` — Blender atölyesi teslimini (`manifest.json` + glb/fbx) `Games/<Oyun>/Art/<Tür>/<id>/`e alır (`{"manifest":"<mutlak yol>"}`); aynı id tekrar gelince GUID korunur; Unity'nin ölçtüğü üçgen/kemik/klip sayısını manifest'le karşılaştırır (`matches`). Test fikstürü `Tools/fixtures/handoff-cube/`.
  - `ai_toon_material` — prefabın her renderer'ına `Project/Toon` materyali: `{"prefab": "<yol>", "texture"?: "<doku>", "model"?: "<glb/fbx, ilk base-color dokusu>", "material"?: "<.mat>"}` (varsayılan `<prefab>_Toon.mat`). Ayarlar (gölge rengi/eşiği, kenar, dış hat kalınlığı) materyalde.
  - `ai_build` — oyunun `GameProfile`'ından cihaz derlemesi: `{"action":"start","game":"Pota","platform":"ios"|"android"|"mac","scene"?:"ColorHunt","development"?:true}` → `Builds/<Oyun>-<platform>` (iOS'ta Xcode projesi). Derleme editörü dakikalarca meşgul eder; sonuç `{"action":"status"}` ya da `Temp/ai_build_status.json`. Profilin ürün adı/bundle id/yön/takımı yalnız derleme süresince Player Settings'e yazılır; bitince atölyenin kendi ayarları ve editörün önceki platformu geri gelir (ProjectSettings değişmeden kalır). Derleme URP asset'lerindeki shader ön-eleme alanlarını Unity'nin kendisi günceller, normal. Development derlemelerinde `DevSceneSwitcher` üstte "→ sonraki sahne" düğmesi koyar; tek kurulumla oyunun tüm sahneleri denenir.
  - Ekran görüntüsü için köprünün `manage_camera` aracı yeterli olduğundan ayrı araç yazılmadı.
  - Oyuna özel araçlar oyunun README'sinde (Pota: `ai_basket_*`, `ai_colorhunt_*` (`ai_colorhunt_record`: kare kare kayıt + events.json), `ai_potalook_import` (Pota'dan dışa aktarılan görünüm varlıklarını kurar, oda yansımasını pişirir), `ai_potaflow_build_scene`, `ai_potaflow_record`, `ai_character_setup`). Pota | Unity karşılaştırması: `Tools/potalook/` (`run_export.sh`, `record-pota.mjs`, `compare.py`).
  - `Tools/frame_sheet.py` — kare klasörlerinden (`..._t<sn>...png`) zamanlı karşılaştırma levhası: `uv run --with pillow Tools/frame_sheet.py OUT.jpg Etiket=klasör ... --times 0.2,0.8,...`.
- **Hazır efektler** (`Assets/_Project/Core/VFX/`, `VfxService.Play("hit"|"explosion"|"pickup"|"swish"|"multiply"|"add"|"rim_hit", pos, rot)`): prefab + `VfxDefinition` + `VfxLibrary.asset`, `UnityEngine.Pool` ile havuzlanır, sahneye `VfxService` objesi konur. Global Volume: Bloom 1.0 / eşik 0.9, Vignette 0.25, Color Adjustments (kontrast +10, doygunluk +8). Yeni efekt: prefab + `Project/VFX/VFX Definition` asset'i + kütüphaneye ekle.
- **Juice bileşenleri** (Core, ad alanı `Project.Juice`): `CameraShake`, `TimeScaleController` (`Time.timeScale`'i doğrudan yazma, her karede geri alır; yavaş çekim/hit-stop için `SlowMo(scale, s)`), `PopupText` (Canvas/Font boşsa kendisi kurar), `SynthSfx` (sentez ses efektleri).
- **Testler:** EditMode `Core/Tests/EditMode` + oyunların `Tests/EditMode`'u; PlayMode `Core/Tests/PlayMode` (`SceneBootTests`: Build Settings'teki her sahne diskten açılıp 3 sn hatasız çalışmalı) + oyunların `Tests/PlayMode`'u Tek komut: `~/.local/bin/uv run Tools/unity_tests.py EditMode|PlayMode [Project.Tests.Fixture ...] [--no-compile]` (Unity'ye odak verir, Play'i durdurur, derletip derleme bitene kadar bekler, `error CS` varsa çıkış 2; başarısız iş `result` yerine `progress.failures_so_far` döndürür, onu okur; 0 test gelirse bir kez yeniler; köprü eski bir işi "running" sanıp meşgul kalırsa betik yeniden yükletir). Sahne ya da sahne aracı değişince PlayMode'u da çalıştır. 2026-09-30 (Renk Avı Pota görünümü sonrası): EditMode 158/158, PlayMode 43/43. Odak yokken PlayMode "0 test" bulabiliyor ya da iş takılıyor (bir kez "Cancelled by user" göründü): çalıştırıcı bu yüzden önce `open -a` ile odak verir.
- **Genel dersler (oyunlardan çıkan):**
  - Play Mode içinde script derleme YAPMA: domain reload havuzu/abonelikleri bozuyor ve konsolun hata-duraklatması editörü "Pause"a alabiliyor (`EditorApplication.isPaused`). Önce `manage_editor stop`. Oturum başında Editor Play'de kalmış olabilir.
  - Araçla kurulan her referans `[SerializeField]` olmalı: Edit Mode'da atanan `private` alanlar serileştirilmez; aynı oturumda çalışır, editör yeniden açılınca null olur (Renk Avı "busy" kilidi, 2026-09-23). Olay dinleyicilerini sar ki UI hatası oyun akışını durdurmasın.
  - Editörün oyun döngüsü ancak bir Game view çizilirken ilerler; ana pencere arka plandayken `Time.frameCount` durur. Kare kare kayıt için kendi Game view'ını aç, Pause + `EditorApplication.Step()` (örnek: Pota `ai_basket_record`). Hızlı yol (2026-09-29): her `execute_code` çağrısında `EditorApplication.isPaused = true; EditorApplication.Step();` bir kare ilerletir (`open -a` odak veremedi). Aksi hâlde Play'de yeni eklenen Animator T-pozunda kalır.
  - Edit Mode'da elle değerlendirilen PlayableGraph'tan sonra `Camera.Render()` bayat deri çizer (skinning oyuncu döngüsünde güncellenir); `forceMatrixRecalculationPerRender` yetmedi, `SkinnedMeshRenderer.BakeMesh` + MeshRenderer kopyası çiz.
  - `execute_code` içinde uzantı metotları (`SetTime`, `SetSourcePlayable`) `using` olmadan görünmez; statik çağır: `UnityEngine.Playables.PlayableExtensions.SetTime(p, t)`.
  - Yeni materyali diske (`CreateAsset`) ancak bütün ayarlarını yaptıktan sonra yaz; önce yazıp sonra bellekte doku atamak, prefab kaydında diskten yeniden yüklenip kaybolabiliyor (kararsız test, 2026-09-29).
  - TextMesh etiket + gölge aynı render kuyruğundaysa çizim sırası rastgele: etiket 3002, gölge 3001. Dinamik font atlası yeniden kurulunca TextMesh UV'leri bayatlıyor: `Font.textureRebuilt`te metni yeniden ata, karakterleri `RequestCharactersInTexture` ile önceden iste.
  - `AddComponent<ParticleSystem>` hemen oynamaya başlar; `duration` yazmadan önce `Stop(StopEmittingAndClear)`.
  - `manage_camera screenshot` varsayılan olarak `Assets/Screenshots/` altına async yazar; `output_folder: "Temp/mcp_out"` ver (git'e girmez). Kalıcı görseli `Tools/keep_screenshot.sh <kaynak> <ad>` ile kaydet: `docs/screenshots/<ad>.jpg`, en uzun kenar 1280, JPEG 80 (~150 KB; tam boy PNG 4–5 MB idi).
  - Aktif platform iOS iken Test Runner PlayMode testlerini hiç bulmuyor ("0 test", 2026-09-28); editör Mac (StandaloneOSX) platformunda kalmalı. `ai_build` bu yüzden geri döner; elle geçiş yaptıysan `EditorUserBuildSettings.SwitchActiveBuildTarget(Standalone, StandaloneOSX)`.
  - Unity'yi kapatmak gerekirse: önce kaydedilmemiş sahne olmadığını kontrol et, sonra `execute_code` (`safety_checks=false`) içinde `SynchronizationContext.Current.Post(_ => EditorApplication.Exit(0), null)`. `osascript ... quit` Unity'yi yarı kapalı bıraktı: `Temp/` ve kilit dosyası silindi, editör çalışmaya devam etti (2026-09-28).
  - Platform değişince köprü paketi yeniden derlenir ve kendi kodundan `UAC0005` uyarıları çıkar; bizim kodumuz değil, gürültü.
  - Sahne aracının Edit Mode'da `onClick.AddListener` ile eklediği dinleyiciler sahneye kaydedilmez: Renk Avı okları ve "Yeni tahta" diskten açılan sahnede ölüydü (2026-09-29). Düğmeleri çalışma anında bağla (`Bind` içinde, adla bul, `RemoveAllListeners` + `AddListener`).
  - Screen Space Overlay UI bir kameranın RenderTexture'ına hiç düşmez: kayıtta HUD görünmesi için canvas'ları kayıt süresince Screen Space Camera'ya al (`Games/Pota/Editor/FrameRecorder.cs`). O sırada canvas dönüşümü bir kare bayat kalır; UI'ı ekran pikselinden canvas'a çevirerek konumlama (`ScreenPointToLocalPointInRectangle` absürt değer verdi), 0–1 çapa (anchor) ile yerleştir.
  - ffmpeg Homebrew'da kurulu ama Claude'un kabuğunda PATH'te değil: `/opt/homebrew/bin/ffmpeg` (drawtext yok; yazıyı PIL ile görüntüye bas). Node: `~/.nvm/versions/node/v22.23.2/bin/node`.
  - Pota (three.js) karşılaştırması: Pota salt okunur; `rsync -a --exclude .git ~/projects/Pota/ <scratch>/pota-copy/` + kopyada vite (başka port) + scratch'te kendi Playwright betiğin. Pota'nın `arac/` betikleri kendi `onizleme/`sine yazar, çalıştırma. Playwright `recordVideo.size` görüntü alanından büyükse sayfa sol üst köşeye düşer: aynı boyu ver ya da kırp.
  - Unity arka plandayken `EditorApplication.delayCall`/`update` uzun süre hiç çalışmayabilir (derleme kuyruğu "queued"da kaldı). İşi sonraya bırakırken `SynchronizationContext.Current.Post(...)` + `EditorApplication.QueuePlayerLoopUpdate()` kullan; köprünün komutları da bu yoldan çalışır.
  - **URP son işlemi kamera başına ve varsayılan kapalı:** 2026-09-30'a kadar hiçbir Pota sahnesinde açık değildi, her Global Volume boşa duruyordu (`SceneParts.EnablePost`). Açınca projenin varsayılan volume profili de devreye girer (vinyet 0,25, bloom, tonemapping): görünümü değişmemesi gereken yerde bunları sıfırlayan düşük öncelikli bir "Neutral" volume koy (`PuzzleLighting`).
  - Gerçek zamanlı reflection probe'lar iki kalite seviyesinde de kapalı: odayı editörde kur, `Lightmapping.BakeReflectionProbe` ile EXR'a pişir, Custom probe'a ver (`PotaRoomBaker`). Pişirme yalnız `ReflectionProbeStatic` işaretli objeleri çizer; işaretsizse küp simsiyah çıkar.
  - Parçacık köşe rengi 8 bit: HDR efekt rengini renk ÷ tavan olarak köşeye, tavanı materyal şiddetine ver (`ParticleSystemRenderer.applyActiveColorSpace = false`, değer doğrusal).
  - three.js → URP: ışık yoğunlukları ÷π; three renkleri doğrusal uzayda karıştırır, MaterialPropertyBlock'a doğrusal değer (`SetVector`) ver.
  - Duraklatılmış editörde her `EditorApplication.Step()` `Time.fixedDeltaTime` (0,02 sn) ilerletir, `captureDeltaTime`'ı yok sayar: kare kare kayıtta kareleri gerçek oyun zamanıyla adlandır (`ai_colorhunt_record`), yoksa oyun 1,67× yavaş görünür.
  - Playwright videosu `t0`'dan ~0,35 sn sonra başlar: karşılaştırmada her olayda köşede bir işaret yak ve anları işaretten oku (`Tools/potalook/compare.py`). ffmpeg EXR'ı 1,0'da kırpar; HDR değerleri `uv run --with OpenEXR --with numpy` ile oku.
- **Karakter hattı (atölye, 2026-09-25):** `kahraman.png` → nano-banana-pro A-pozu → `firtoz/trellis` GLB → `uthana/create-character-v1` rig → `uthana/text-to-motion-diffusion-v2` klipler → Unity Humanoid. Script'ler ve dersler `Tools/character/README.md`, özet `docs/2026-09-25-character-pipeline.md`, görüntüler `docs/screenshots/2026-09-25-character/`. Ücretli çıktılar `Tools/character/out/` (git dışı, silme). Unity adımı `ai_character_setup` şimdilik Pota'da, çünkü varsayılanları Pota'ya göre. **2026-09-29'dan beri** görünüm ve model adımları copy'de (`cli/gorunum.ts`, `cli/mesh.ts --model`), temizlik/retopo/doku Blender atölyesinde (`character_model`); burada kalan: iskelet ve hareket (alt proje 3'e kadar), `ai_character_setup`, `ai_toon_material`.
  - Tuzaklar: Uthana rig'ini glTFast doğrudan okuyamaz → önce `fix_rig_glb.py`; TRELLIS.2 WebP doku verir (`glb_webp_to_png.py`) ve min 100k üçgen ister; Avatar asset'ini üzerine yazma (önce sil), prefab'ı avatar/controller sonrası `ImportAsset(ForceUpdate)` ile yeniden yükle; `execute_code` içinde `AssetDatabase.DeleteAsset` `safety_checks=false` ister.
- **Oyunlar:**
  - **Pota** (`Assets/_Project/Games/Pota/`, notlar `Games/Pota/README.md`; Pota üzerinde çalışırken önce onu oku): Basket turu bitti, animasyonlu oyuncu eklendi; Renk Avı denemesi Tuna'nın bakışını bekliyor (patlama/efekt hissi, tahta boyu, HUD). **PotaFlow** (2026-09-30): Basket turundan Renk Avı tahtasına tek sahnede kesintisiz geçiş (`Scenes/PotaFlow.unity`); Tuna A "Akan toplar"ı seçti, toplar kahramanın yanındaki sepete dolar (`docs/screenshots/2026-09-30-potaflow-basket-side.mp4`).
  - **Kaptan** (`~/projects/Kaptan`): bu şablondan kopyalanmış ayrı Unity projesi. Ortak araçların orada ayrı bir kopyası var; burada düzeltilen ortak kod oraya kendiliğinden gitmez. Üçüncü Unity oyunu gelince `Core/`u iki projenin de bağlandığı yerel bir pakete çevir.
- **Aktif oyun / mevcut aşama:** Pota: Basket turunun oyuncusu artık toon kahraman `Games/Pota/Prefabs/PotaHeroToon.prefab` (Tuna onayladı, 2026-09-29). PotaFlow A hazır (toplar kahramanın sepetine). Renk Avı Pota'nın bulmaca görünümüyle birebir bitti (2026-09-30; karşılaştırma `docs/screenshots/2026-09-30-potalook-*`). Sıradaki: "üstüne inşa" adayları (ayrı spec, Tuna "daha iyi" adaylar arasından seçecek); Pota'nın 2026-09-30 04:29 tasarım turundaki alt bar + ipucu yeri henüz taşınmadı. Stüdyo hattı: Blender atölyesi ve karakter model hattı bitti (ikinci turda "ucuz diye kötü yapma" ilkesiyle yeniden oturtuldu). Sıradaki alt proje iskelet + animasyon (`~/projects/Blender MCP/docs/studyo.md`); yeni bütçe onayı gerekecek.
