# Pota'ya toplu teslim: his katmanı (F1–F13 + M1–M11)

_2026-10-03. Bu metin Pota oturumuna (`~/projects/Pota`, three.js + DOM) başlangıç mesajı olarak yapıştırılır. Kaynak: Unity MCP atölyesi._

---

Tuna'nın isteğiyle Pota'nın "his" katmanı Unity atölyesinde tasarlandı, videolarla onaylandı; şimdi Pota'ya aktarılacak. İki paket, hepsi kabul edildi:

1. **Oyun içi anlar F1–F13** (Renk Avı + basket turu): `~/projects/Unity MCP/docs/handoff/2026-10-03-pota-oyun-ici-his.md`
2. **Menü ve ödül anları M1–M11** (pencereler, para uçuşu, kazanma/kaybetme, harita, ana ekran, terfi, bonus, market, öğretici eli): `~/projects/Unity MCP/docs/handoff/2026-10-03-pota-menu-odul-his.md`

Önce iki belgeyi oku. Her anın **ne olduğu, saniyeleri, eğrileri ve referans kodu** tablolarda; Unity'deki saf sınıflar (`WinSequence`, `MapProgress`, `ReelSpin`, `FeelCurves`, `MetaCurves`) zamanlamayı t → durum olarak verir, doğrudan TypeScript'e çevrilebilir (`~/projects/Unity MCP/Assets/_Project/Games/Pota/Scripts/`). Videolar: `~/projects/Unity MCP/docs/screenshots/2026-10-03-feel-all.mp4` (Önce | Sonra) ve `…/2026-10-03-meta-all.mp4` (Pota | Önce | Sonra); tek tek `…-feel-<an>-side.mp4`, `…-meta-<an>-side.mp4`.

## Tuna'nın kararları
- F1–F13 ve M1–M11'in **hepsi** giriyor; tek değişiklik: **nefes alıp verme yok** — ana ekrandaki görsel nefes almayacak, marketteki kahramanın bugünkü `vitrinNefes` animasyonu da **kaldırılacak**. Kahramanın zıplaması (deneme, satın alma kutlaması) kalıyor.
- Ses, müzik, titreşim bu turda **yok**; var olanlara dokunma.
- Kurallar ve ekonomi **değişmez** (ödül 15 + ilk deneme 10 + kalan top parası `min(20, floor(kalan×0,1))`, madalya/lig eşikleri, bonus ağırlık ve eşikleri, katalog fiyatları). Görünüm Pota'nın görünümü; yeni görsel yok, yalnız hareket ve zamanlama.

## Yol boyunca düzeltilecek iki Pota hatası (belgelerin §4'ü)
- `kazandi`/`kaybetti` sesi ve titreşimi iki kez çalıyor (`renkAviFaz.ts:940` + `akis.ts:1095/1099`); birini kaldır.
- Kazanma penceresindeki lig çubuğu (`ligCubukYaz`) pencere gizliyken yazıldığı için 0,6 sn geçişi büyük olasılıkla hiç oynamıyor; M4 ile birlikte çöz.

## Önerilen sıra (her adım tek commit, önce ucuz ve her yerde görünen)
1. **M1** pencere/düğme kiti (örtü 0,18; kutu 0,85 → 1,05 → 1 / 0,28; şerit düşüşü; basma 0,94 + 3B gölge, bırakma 1,04) — bütün pencereler ve düğmeler bundan yararlanır.
2. **M3** para uçuşu + dönen sayaçlar (8–12 para, kavis, varışta rozet 1,25, son parada parıltı; harcamada rozet döner) — M4, M9, M10 ve F3 bunu kullanır.
3. **M2** sayfa kaymaları, sekme yükselmesi, "Oyna" karartması.
4. **M4** kazanma penceresi (damga, 0,8 sn sayaç ve **yükselen madalya**, kupa kartı, lig çubuğu, +N para uçuşu, Devam nabzı) ve **M5** kaybetme (sallanma, kalan sayı damgası, "Kıl payı!", nabız).
5. **M6** harita ilerlemesi (Devam → harita; yeşil düğüm + madalya, yol noktaları, "Sen" pini 3 zıplama, kilit açılışı; toplam 2,1 sn) — düğüm formülü ve `senYeri` zaten Pota'nın.
6. **M7** ana ekran (nefes **yok**; Oyna parıltısı 2,2 sn'de bir, rozet girişi, bulut sprite'ları), **M8** terfi/lig, **M9** bonus (ezilme + taşma, altın hücreler, kazanç paralarının karta uçuşu, "olmadı" sallanması), **M10** market (satın alma kutlaması, deneme flaşı, yetmez sallanması + rozet kırmızı), **M11** öğretici eli.
7. **F1–F9** Renk Avı (bölüm başı şeridi + hedef uçuşu, az top gerilimi, kalan topların finali, zincir kelimeleri, boşta ipucu, iniş yaylanması, çerçeve maskesi/`clippingPlanes`, canlı gökyüzü, kaybetme anı) ve **F10–F13** basket (sapan, ıska "KAÇTI!", "SWISH!", sayım damgası).

## Varlıklar (Blender atölyesi teslimleri; PNG/GLB doğrudan kullanılır)
`~/projects/Blender MCP/assets/Pota/<id>/export/`: `vfx-hand-pointer` (öğretici eli, 512²), `vfx-cloud-still` (menü bulutu, 512×256), `vfx-letter-miss` (KAÇTI! levhası), `cloud-set`, `bird-flock` (`flap` klibi), `city-band` (glb, +Y yukarı, metre), `vfx-coin`. Kopyala, Blender klasörüne yazma. Market forma rengi için `kahraman-maske.png` seviyeleri: forma 36, şort 72, saç 144, top 216.

## Çalışma kuralları
- Her anı bitirince Pota'nın kendi önizleme akışıyla (`arac/`, `onizleme/`) kare/video al; Tuna oradan bakacak. Bir anı geçici olarak kapatabilen tek bir `HIS` anahtarı (karşılaştırma için) faydalı olur; kabul sonrası kaldırılabilir.
- Pota'nın mevcut dosya/isim düzenine uy; yeni bağımlılık ekleme (tween kütüphanesi gerekmez, CSS keyframe + rAF yeter).
- Sayılar belgelerdeki tablolardan; tereddütte Unity kodu bağlayıcı, videolar hakem.
- Bitince: `docs/` içindeki Pota günlüğüne kısa bir "his katmanı" notu ve Tuna'ya hangi sahnede nasıl deneyeceği.
