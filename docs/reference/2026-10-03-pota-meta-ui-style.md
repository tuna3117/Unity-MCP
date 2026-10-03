# Pota menü/ödül katmanı — arayüz stil ve yerleşim rehberi (2026-10-03)

Pota'nın DOM arayüzünden (`~/projects/Pota/index.html` `<style>` 10–537, markup 539–859; metinler `src/dil.ts` tr 10–106) çıkarıldı.
Ölçüler 390 CSS px genişlikteki telefon için. Unity'de `Scenes/Meta.unity` bu rehbere göre kurulur (2. alt proje).

## 0. Globaller
- `:root`: `--lacivert #142850` (panel/metin), `--lacivert2 #1f3a6e`, `--turuncu #ff7a1a`, `--turuncu2 #d95a05` (büyük rakamlar), `--altin #ffc83d`, `--altin2 #b3600a` (3B alt gölge), `--yesil #2fb36b`, `--yesil2 #176b3e`, `--krem #fff4e0`, `--ust max(14px, safe-top)`, `--alt max(10px, safe-bottom)`.
- Gövde yazısı Fredoka 600 (dosyalar 500/700); başlıklar Lilita One 400, harf aralığı .3px; açılış Sora. Lilita One'da ğ/ş/İ yok.
- Pencere örtüsü `.pencere`: tam ekran `rgba(8,16,40,.62)`, ortalı, padding 24.
- `.kutu`: genişlik min(88vw, 380) = 343; dikey gradyan `#fff8ea → #fff4e0`; kenar 4px altın; yarıçap 30; gölge `inset 0 0 0 3px #fff` + `0 8px 0 #b3600a` + `0 22px 44px rgba(0,0,0,.45)`; içerik padding 16 20 26, boşluk 12.
  - Şerit başlık `h2`: kutudan 24 yukarı ve 12 dışarı taşar; padding 10 24; Lilita 28 beyaz; gradyan `#ffb35c → #ff7a1a 50% → #d95a05`; kenar 3 #fff; yarıçap 22; gölgeler `inset 0 3px 0 rgba(255,255,255,.35)`, `0 5px 0 #8f3a00`, `0 10px 18px rgba(0,0,0,.3)`.
  - Kapat `.kapat`: sağ üst (−18, −16), 46 daire, kenar 3 #fff, gradyan `#ff6b6b→#d92b36`, "×" 24 w900, gölge `0 3px 0 #7a1219`.
  - Beyaz kart `.panel`: #fff, lacivert metin, yarıçap 18, padding 14, Fredoka 800, gölge `0 3px 0 rgba(20,40,80,.12)`; `.buyuk` 52 w900 turuncu2; `.kucuk` 14 w700 .8.
- Düğmeler: `.buyukDugme` kenar 3 rgba(255,255,255,.55), yarıçap 22, padding 14 44, Lilita 28 beyaz, yeşil gradyan `#7ae3a0 → #2fb36b 50% → #219a58`, gölge `inset 0 3px 0 rgba(255,255,255,.35)`, `0 6px 0 #176b3e`, `0 10px 18px rgba(0,0,0,.3)`; basılı: 4px aşağı, gölge `0 2px 0`. `.turuncu` `#ffb46b → #ff7a1a → #d95a05` (3B `#8f3a00`), `.mavi` `#5b86e0 → #2b55c7 → #1d3f8f` (3B `#122a63`). `.kucukDugme` yarıçap 12, padding 8 14, Fredoka 900 14, `#5fd48a→#2fb36b`, `0 3px 0 #176b3e`. `.rozet` hap: gradyan `#24407a → #142850`, kenar 3 altın, Lilita 17, ikon 22, gölge `0 3px 0 #b3600a`. `.yuvarlak` 44 daire. `.arti` 22 yeşil daire "+".
- Keyframes: `pop` .18s (%40: 1.25, altın), `nefes` 1.6s, `salla` .35s ±6/±4, `zipla` 1.2s (%50: −8, 1.08), `dus` konfeti 2s, `nabiz` 1.4s (halka 14px rgba(255,200,61,.4)), `nabizDugme` 1.8s (1.035), `bildir` 1.6s, `parmak` 1s (1→.72, .95→.6).

## 1. Açılış ve yükleme
Açılış: Sora, koyu radyal zemin, prizma SVG, "AETHRODE" harf harf, 2700 ms. Yükleme: `acilis.jpg` kapak; logo "SWISH / & / SMASH" Lilita 58 altın, 3px lacivert kontur, −4°; alt "SOKAK BASKETİ" 17; çubuk 265×20 lacivert, kenar 3 altın, dolgu turuncu gradyan; "Yükleniyor…" 18.

## 2. Ana ekran `#menu`
Zemin bölüme göre `anaekran-1-gunduz/2-gunbatimi/3-gece/4-sokak.jpg` (1536×2752) + üst/alt karartma gradyanı. Üst şerit: avatar `.yuvarlak` 50 (`kahraman.png` üstten) + seviye hapı (turuncu, kenar 2 altın, 12 w900); sağda lig rozeti (`lig-*.png` 26 + kupa sayısı kısa "13,5K"), para rozeti (`para.png` + sayı + `.arti`), can (`kalp.png` + "TAM"/sayı + süre), dişli `.yuvarlak` 44. Alt: Oyna `.buyukDugme` 34, padding 16 62, kenar 4 altın, yarıçap 26, `nabizDugme`; metin "Bölüm {n}". Sekme çubuğu: gradyan `#24407a→#142850`, üst kenar 4 altın; `.sekme` Lilita 13 .8, ikon 50 (`market.png`, `ev-ikon.png`, `harita-ikon.png`), "Market / Ana / Harita"; seçili: ikon −14px ×1.25, altın kart (yarıçap 18 18 8 8, gradyan `#ffd66b → #ffc83d → #e0a12a`, kenar 3 #fff, `0 4px 0 #b3600a`), yükseklik ≈ 86 + safe.

## 3. Harita `#harita`
Zemin `sehir.jpg` + açık gradyan; başlık şeridi "Harita" (26, padding 8 40) + kapat; üstte 56px siyaha karışan maske. 50 düğüm: `x = W/2 + sin(n·0.85)·W·0.3`, `y = 90 + (50−n)·118`. Yol: beyaz noktalı çizgi (kalınlık 13, `1 24` kesik, gölge `0 3px 0 rgba(20,40,80,.45)`). `.dugum` 72 daire, kenar 4 #fff, Lilita 27, gölge `inset 0 3px 0 rgba(255,255,255,.35)`, `0 6px 0 rgba(0,0,0,.28)`, `0 10px 16px rgba(0,0,0,.25)`; geçildi: yeşil gradyan + madalya 30 (sağ alt −10) ya da tik; şimdi: turuncu gradyan + `nabiz`; kilitli: gri `#7d8aa3→#4e5a70` + kilit SVG 22. "Sen" pini: 58 daire `kahraman.png`, kenar 3 altın, altında "Sen" hapı; düğümün 78 px yanında, 8 px yukarı.

## 4. Bölüm penceresi `#bolumPencere`
Şerit "Bölüm {n}"; para rozeti sağa (14, para 20); HEDEF kartı (etiket 11 w800 .7; `.hedefKutu` eşya 34 + sayı 24 Lilita turuncu2, boşluk 14); iki `.bilgi` kartı (`top.png` 38 + N + "CEPHANE", `para.png` + "+15" + "ÖDÜL"); en iyi/madalya satırı; güç çipleri (`guc-altin.png`, `guc-cephane.png`; beyaz, yarıçap 14, seçili altın kenar + `#fff6dc`); düğmeler tam genişlik: "Oyna · {n} top" turuncu, "Basket at" mavi (`sepet.png` 30) + ❤1 rozeti.

## 5. Kazanma `#kazandi`
Konfeti (70 parça 10×16, renkler `#ff7a1a #ffc83d #2fb36b #3a8dff #ff4d6d #fff`, 1.6–2.8 s). Şerit "Bölüm {n} bitti"; `h1` "Harika!" 44 Lilita altın, kontur 2 `#b3600a`, gölge `0 5px 0 #b3600a`, −3°; SKOR etiketi + `.skor` 44 turuncu2 (0→toplam 800 ms, sonra `pop`); kalemler satırı .7; madalya 40 + ad 18 w900; `.panel` "Kupa +N"; lig çubuğu (kart 14, ikon 44, ad, çubuk 10 `#e3e9f4` dolgu altın→turuncu .6 s, "Sonraki lige {n} kupa"); ödül: `para.png` 64 `zipla` + "+15" 44 altın; `.panel` "{n} top arttı · +N ödül · ilk denemede!"; "Devam" yeşil.

## 6. Kaybetme `#kaybetti`
Kapat; şerit "Cephane bitti"/"Hamle kalmadı"; `.panel` `.buyuk` kalan hedef 52 turuncu2 + "hedef tamamlanmadı"; "Yeniden dene" (❤1) turuncu/mavi, "Basket at" (`sepet.png`) mavi/turuncu (tek turuncu), "+16 top · [para] 100" yeşil, "▶ Video izle · +16 top".

## 7. Terfi `#terfi` ve lig `#ligPencere`
Terfi: konfeti; şerit "Yeni lig!"; `lig-*.png` 140; `h1` lig adı (altın stil); `.panel` "{lig}'ne yükseldin!"; "Devam" turuncu. Lig penceresi: ikon 96 + ad 26 + kupa sayısı; 5 basamak (Şampiyonlar üstte; beyaz, yarıçap 14, ikon 40, ad 15, eşik/✓/"SEN"); eşikler 0/12400/44700/119100/154200; adlar Çaylak/Amatör/Profesyonel/All-Star/Şampiyonlar Ligi.

## 8. Bonus `#bonus`
Şerit "BONUS"; n şerit × 3 hücre (56), yarıçap 12, beyaz, `inset 0 0 0 2px altın`; hücre ikon 40 ya da "+N" 22 turuncu2; dönüş translateY (12+satır)·56 px, 1400 ms + 120/sütun, `cubic-bezier(.25,.9,.3,1)`; kazanan hücre altın parıltı + `pop` ×2, diğerleri .4; "BONUS OYNA · 3" turuncu, "Kapat" yeşil; not "Kazanç beklemede: bölümü geçince alınır".

## 9. Market `#market`
Şerit "Market" + kapat; para rozeti; kategori sekmeleri (lacivert hap, Lilita 12 `#cfe0ff`, ikon 34: `kat-giysi/kat-top/kat-guc/para-yigini`; seçili altın gradyan); vitrin: spot ışığı, podyum, `kahraman.png` (maske ile renklenir) %83 yükseklik, `vitrinNefes` 3.2 s, ±20° eğme; yuva sütunu 44 (`giysi-*.png`); deneme paneli krem, üst kenar 4 altın, yarıçap 28 28 0 0; çipler 5 sütun (beyaz, yarıçap 14, ikon 50, 12 Lilita, denenen altın kenar); büyük düğme "{ad} · [para] fiyat" / "Giy" / "Giyili"; kartlar `.mKart` (beyaz, yarıçap 18, ikon 64, ad 13, açıklama 11).

## 10. Öğretici
`#parmak`: 46 daire, kenar 4 #fff, halka `0 0 0 6px rgba(255,180,60,.55)`, `parmak` 1 s; `#yIpucu`: krem balon 13 w700 `#103055`, yarıçap 12; basket ipucu krem, kenar 3 altın, `nefes`.

## 11. Ayarlar, toast
Ayarlar: üç `.ayar` 62 kare (yeşil gradyan / kapalı gri), "Müzik / Ses / Titreşim"; dil düğmeleri; sürüm satırları. Toast `#bildirim`: krem hap, Lilita, alt `--alt+150`, `bildir` 1.6 s. Can penceresi: 5 kalp 44, "5 / 5" 52, "Can al".

## 12. Görseller (`public/assets`)
1536×2752: `acilis.jpg`, `anaekran-1..4.jpg`, `sehir.jpg`. 256²: `para`, `disli`, `kalp` (256×222), `kupa` (222×256), `lig-*`, `madalya-*`, `esya-*`, `engel-*`, `ozel-*-a`, `giysi-*`, `guc-*`, `top`, `top-*`. Diğer: `kahraman.png` 553×784 (+`kahraman-maske.png`), `sepet.png` 720×675, `ev-ikon.png` 384×304, `harita-ikon.png` 278×384, `market.png` 256×249, `kat-giysi` 150×256, `kat-top` 252×256, `kat-guc` 210×256, `para-yigini.png` 512×371. Fontlar `public/fonts/` (woff2; Unity için Lilita One TTF projede, Fredoka TTF Google Fonts'tan OFL).
