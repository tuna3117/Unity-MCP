# Blender atölyesi — tasarım (2026-09-28)

Tuna'nın hedefi: Pota, Tekne/Kaptan, simülasyon ve bulmaca oyunları için **büyük mobil stüdyo kalitesinde**
karakter, animasyon ve efekt üretebilmek; Unity ve Blender'la, açık neredeyse orada Blender'ı kullanarak.
Yolu ve kararları bana bıraktı ("tüm kararları sana bıraktım"). Bu belge o yolun ilk alt projesini, Blender
atölyesini tanımlar; sonraki alt projeler §9'da.

## 1. Kalite hedefi (ölçüt)

- Karakter ve animasyon: **Squad Busters / Brawl Stars** çizgisi. Stilize, büyük kafalı oranlar, küçük ekranda
  okunan net şekiller, toon ışık + dış hat + kenar parlaması; hazırlık, vuruş ve sekmesi belirgin animasyon.
- Bulmaca efektleri: **Royal Match / Toon Blast** çizgisi (patlama, zincir, hedefe uçan parça, sarsıntı).
- Simülasyon karakterleri: **Hay Day** çizgisi, sade ve sıcak.
- Başarı: aynı kadrajda referansın yanına konan karşılaştırma görüntüsü + kare kare inceleme, son söz Tuna'nın
  gözü. Stilize karakterde bu çizgiye yaklaşmak gerçekçi; en zor kısım animasyonun canlılığı.

## 2. Açık analizi (neden Blender)

| Adım | Şu an | Açık | Kim kapatır |
|---|---|---|---|
| Konsept | copy + yapay zekâ görseli | küçük | copy |
| 3B model | TRELLIS: ~100k üçgen yığın, ışığı gömülü doku | büyük | Blender (sadeleştirme, temizleme, stilize malzeme) |
| Görünüm | düz URP Lit | orta | Unity (toon/dış hat shader'ları) |
| İskelet | Uthana otomatik, yüz yok | orta–büyük | Blender (düzeltme, gerekirse yüz/ifade) |
| Animasyon | yapay zekâ hareketi, koşu/smaç zayıf | en büyük | Blender (temizleme, abartma) + Unity (kodla ikincil hareket) |
| Efekt | düz parçacık, varsayılan doku | büyük | Blender (flipbook doku, efekt mesh'i) + Unity (shader) |

## 3. Atölyeler arası mimari (onaylandı)

Ayrı klasörler, **tek şef oturum**, dosyayla teslim. Ajanlar arası mesajlaşma yok; paralel iş gerekirse şef alt
ajan açar.

```
copy (uretim)            Blender MCP (yeni)                  Unity MCP / three.js oyunları
yapay zekâ çağrıları,    model, temizleme, iskelet,          içe aktarma, shader, efekt
2D görsel, 3B taslak,    animasyon düzeltme, efekt dokusu,   kurgusu, oyun, test, derleme
köken + kalite           render ile göz kontrolü
        └──────── teslim: export/<id>.glb|fbx + manifest.json + önizleme ────────┘
```

- Blender ayrı depo, çünkü yalnız Unity'ye hizmet etmiyor (Pota three.js'te glb kullanır), `.blend` dosyaları
  büyük ikili dosyalar (Unity klasöründe içe aktarılmaya çalışılır) ve her atölyenin CLAUDE.md'si kendi işine
  odaklı kalmalı.
- Yapay zekâ servis çağrıları copy'nin işi. Unity MCP'deki `Tools/character` (TRELLIS/Uthana/text-to-motion
  çağrıları) karakter hattı alt projesinde copy'ye taşınır; Unity MCP'de yalnız Unity'ye aktarma adımı kalır.
- `copy` git deposu değil: bu alt projede yerel `git init` yapılır (`.gitignore` zaten `.env`, `node_modules`,
  `dist`, `cikti` dışlıyor). Hiçbir yere gönderilmez.

## 4. Blender atölyesinin yeri ve düzeni

`~/projects/Blender MCP` (Unity MCP ile aynı adlandırma), yerel git deposu, uzak depo yok.

```
Blender MCP/
  CLAUDE.md              Blender çalışma kuralları, köprü notları, dersler (Unity MCP CLAUDE.md'sinin kardeşi)
  docs/studyo.md         stüdyo haritası: copy → Blender → Unity/three.js, teslim sözleşmesi, kim ne yapar
  tools/bl/              Blender içinde import edilen yardımcı paket (sahne, önizleme, ölçüm, dışa aktarma)
  tools/run.sh           arayüzsüz çalıştırıcı: blender -b --factory-startup -P tools/run_tool.py -- <araç> <json>
  recipes/               kodla asset üreten tarifler (her biri bir .py)
  assets/<Oyun>/<id>/    work.blend + export/ (glb/fbx + manifest.json + önizlemeler)
  tests/                 Blender içinde unittest ile çalışan testler (tools/run.sh test)
```

- `.gitignore`: render önbelleği, `*.blend1`, geçici çıktılar. `.blend` ve dışa aktarımlar git'te; tek dosya
  50 MB'ı geçerse Git LFS'e geçilir (şimdilik gerek yok).
- Unity MCP ve copy'nin belgelerine iki satırlık "stüdyo haritası: `~/projects/Blender MCP/docs/studyo.md`"
  işareti eklenir.

## 5. Köprü: resmi Blender Lab MCP

- **Seçim:** Blender Lab'ın resmi sunucusu (`projects.blender.org/lab/blender_mcp`), **v1.0.3 etiketine
  sabitlenir**. Blender 5.1+ ister; kurulu olan 5.2.2 LTS. GPL, ücretsiz, Anthropic'in Blender bağlayıcısının
  kullandığı sunucu. İçinde Python çalıştırma (arayüzlü ve `_for_cli` arayüzsüz), sahne/veri özetleri, alan
  ekran görüntüsü (`get_screenshot_of_area_as_image`) ve paketli Blender API + kılavuz belgesi var.
- **Reddedilen:** topluluk `mcp-for-blender` (eski `blender-mcp`): 2026'da dört güvenlik açığı (CVE) yayımlanmış,
  kullanım verisi gönderiyor, ücretli üretim özellikleri copy ile çakışıyor.
- **Kurulum:** eklenti (`addon/`) Blender'a uzantı olarak kurulur ve otomatik başlatma açılır; sunucu
  (`mcp/`, giriş noktası `blender-mcp`) `uv` ile etiketten çalıştırılır; Claude Code'a **kullanıcı kapsamında**
  kaydedilir ki hangi atölyede açılırsa açılsın şef oturum Blender'a erişsin. Veri yolu:
  `Claude ⇐stdio⇒ blender-mcp ⇐TCP⇒ Blender eklentisi`.
- **Güvenlik:** sunucu gelen Python'u korumasız çalıştırır (Unity'deki `execute_code` ile aynı risk). Kodu
  yalnız ben gönderiyorum, makine yerel; CLAUDE.md'ye yazılır.
- **Köprü yokken:** her araç `tools/run.sh` ile arayüzsüz de çalışır; köprü canlı sahneye bakmak ve Tuna Blender'ı
  açıkken birlikte çalışmak içindir, hat köprüye bağımlı değildir.

## 6. Başlangıç araç seti (bu alt projenin kapsamı)

Her araç `tools/bl/` içinde bir fonksiyon; hem `tools/run.sh <araç> '<json>'` ile arayüzsüz, hem köprüden
`import bl; bl.<araç>(...)` ile çalışır. Sonucu JSON olarak döndürür ve özet satırını yazar.

| Araç | Ne yapar |
|---|---|
| `inspect` | Bir .blend/glb/fbx dosyasını ya da sahneyi ölçer: üçgen, n-gon, manifold olmayan kenar, UV, malzeme, doku çözünürlüğü, kemik sayısı ve adları, boyut (m), orijin, ölçek/dönüş uygulanmış mı, animasyon klipleri. |
| `preview` | Standart göz kontrolü sayfası: önden, yandan, arkadan, 3/4, üstten + tel kafes, tek PNG (EEVEE, sabit ışık/kamera, nötr zemin, ölçek çubuğu); köşesinde `inspect` rakamları. Animasyon verilirse kareler ızgarası + kemik yükseklik tablosu (Unity'deki `ai_anim_contact_sheet` karşılığı). |
| `export` | Oyun motoruna hazır glb (three.js/Unity) ya da fbx (Unity Humanoid): metre birim, Blender'ın +Z yukarısı dosyada +Y yukarıya çevrilir, ölçek/dönüş uygulanmış, gereksiz veri temiz; yanına `manifest.json` ve önizleme yazar. |
| `new_scene` | Boş, birimleri ayarlı (metre, +Z yukarı), önizleme ışığı hazır sahne. |

Model temizleme, iskelet, animasyon ve efekt araçları kendi alt projelerinde eklenir (§9).

## 7. Teslim sözleşmesi (`manifest.json`)

```json
{
  "id": "pota-hoop-stylized",
  "game": "Pota",
  "kind": "prop | character | animation | vfx-mesh | vfx-flipbook",
  "version": 1,
  "files": { "glb": "pota-hoop-stylized.glb", "fbx": null, "previews": ["preview.png"] },
  "units": "m", "up": "+Y (glTF)", "scale_applied": true,
  "stats": { "tris": 1840, "materials": 3, "textures": [], "bones": 0, "clips": [] },
  "source": { "recipe": "recipes/pota_hoop.py", "copy_provenance": null, "inputs": [] },
  "checks": { "non_manifold": 0, "ngons": 0, "uv_missing": [], "passed": true },
  "made": "2026-09-28T14:00:00Z"
}
```

- `source.copy_provenance`, copy'nin köken kaydındaki kimliği tutar (yapay zekâ ile üretilen girdiler için).
- Unity tarafında `ai_import_asset` (Unity MCP Core) manifest'i okur, dosyaları `Games/<Oyun>/Art/<kind>/<id>/`
  altına kopyalar, içe aktarma ayarlarını uygular ve rakamları manifest'le karşılaştırır (üçgen, kemik, klip).
  three.js oyunlarında glb `public/assets` altına kopyalanır.

## 8. Kanıt görevi (bu alt projenin "bitti" tanımı)

1. Köprü bağlı: Blender açıkken sahne özeti ve alan ekran görüntüsü köprüden alınır; Blender kapalıyken aynı
   ölçüm `tools/run.sh` ile arayüzsüz yapılır.
2. Mevcut asset okunur: Unity MCP'deki `player_rig_a.glb` (Uthana iskeletli kahraman) `inspect` ve `preview` ile
   ölçülür, sonuç rapora yazılır (üçgen, kemik, ölçek sorunları görünür olmalı).
3. Kodla bir asset: stilize bir basket potası tarifi (`recipes/pota_hoop.py`: çember, file, pano, direk; düz
   renkli stilize malzemeler) → `preview` → `export` (glb + manifest).
4. Tam tur: `ai_import_asset` ile Unity'ye alınır, `Experiments/` altında geçici bir önizleme sahnesinde mevcut
   Basket potasının yanına konur (Basket sahnesine dokunulmaz), ekran görüntüsü alınır; manifest rakamları Unity'de ölçülenle tutar.
5. Testler: `tools/run.sh test` (Blender içinde unittest) geçer; Unity tarafında `ai_import_asset` için EditMode
   testi geçer.

## 9. Sonraki alt projeler (her biri kendi tasarım → plan → uygulama döngüsüyle)

1. **Karakter model hattı:** Pota kahramanı. copy'de konsept ve önden/yandan/arkadan çizim → copy'de image-to-3D
   (servis karşılaştırması: TRELLIS, Tripo, Hunyuan3D, Rodin) → Blender'da sadeleştirme/temizleme, stilize
   malzeme ve doku → Unity'de toon + dış hat + kenar parlaması shader'ı. `Tools/character` copy'ye taşınır.
2. **İskelet ve animasyon hattı:** otomatik iskelet → yapay zekâ hareketi → Blender'da temizleme ve abartma
   (anahtar kare azaltma, hazırlık/sekme ekleme, ayak kilitleme) → Unity'de kodla ikincil hareket (yay kemikler,
   ezilme-uzama, bakış). Ölçüt: Squad Busters'tan seçilen 3 hareketle kare kare karşılaştırma.
3. **Efekt hattı:** Blender'da duman/ateş/kıvılcım flipbook dokuları ve efekt mesh'leri (kayma, halka, şok dalgası)
   → Unity'de UV kaydırmalı/çözünmeli shader'lar + parçacık kiti. İlk hedef Renk Avı'nın patlama ve zincir
   efektleri, Royal Match karşılaştırmasıyla.
4. **Genelleme:** Kaptan'ın kaptanı ve NPC'ler, diğer oyunlar.

## 10. Kapsam dışı (bu alt projede)

- Karakter modelleme, iskelet, animasyon ve efekt üretimi (sonraki alt projeler).
- Topluluk MCP'sinin 3B üretim entegrasyonları (Rodin/Tripo/Hunyuan): servis çağrıları copy'de kalır.
- Uzak depo, push, Git LFS (gerekmedikçe).
- Blender'da heykel (sculpt) ve elle boyama: yapamadığım işler; gerektiğinde dış kaynak ya da Tuna'nın gözü.
