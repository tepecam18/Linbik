# Temiz kod dönüşümü

10 Eylül 2026 tarihinde yapılan düzenlemeler .NET kütüphanelerini, CLI'ı,
Android istemcisini ve örnek uygulamaları kapsar. Mevcut public .NET tipleri,
metot imzaları, endpoint adresleri ve yanıt zarfları korunmuştur. Hata düzeltmeleri
ve bilinçli davranış değişiklikleri aşağıda ayrıca belirtilmiştir.

## Sorumlulukların ayrılması

| Alan | Düzenleme |
| --- | --- |
| Core / JWT / PASETO | Cookie oluşturma, istemci seçimi, süre hesaplama ve hata yönlendirmeleri Core'daki internal `LinbikAuthEndpointHelpers` içinde ortaklaştırıldı. JWT ve PASETO token imzalama işlemleri kendi paketlerinde kaldı. |
| Core HTTP istemcisi | Ortak JSON istek oluşturma ve API/diagnostic başlıkları tek yardımcıya taşındı. İstek ve yanıt kaynakları işlem sonunda kapatılıyor. |
| Server | Route ve yetkilendirme kaydı, integration event işleyicilerinden ayrıldı. Başarı/hata yanıtı oluşturma ortaklaştırıldı. |
| YARP | DI kayıt tekrarları birleştirildi. HTTP yönlendirme metotlarındaki gereksiz async katmanları kaldırıldı; istek/yanıt kaynakları kapatılıyor. |
| Slices | Mediator önbelleği yanıt tipine göre ayrıldı ve tip güvenli hale getirildi. |
| Source generator | Metadata çıkarma `LinbikSliceGenerator`, kod yazma `LinbikSliceEmitter`, ara model `SliceModel` içinde ayrıldı. Flow metadata üretimindeki tekrar kaldırıldı. |
| CLI | Status komutu küçük sorumluluklara ayrıldı; JSON okuma tek noktaya taşındı. Ayar dışa aktarımı diğer bölümleri ve istemcileri koruyor. |
| ASP.NET örneği | Tekrarlanan servis kontrolleri ortak metoda ve `ServerCheckResult` modeline taşındı; `dynamic` kullanımı kaldırıldı. |
| Android | Activity'deki HTTP ve JSON işlemleri `LinbikAuthHttpClient` içine taşındı. İstemci bağlantı havuzu tekrar kullanılıyor. |
| Nuxt | JWT doğrulama ve cookie işlemleri sunucu middleware/utility katmanına taşındı. Vue sayfası yalnızca görüntüleme yapıyor; boş catch blokları ve gereksiz loglar kaldırıldı. |

## Düzeltilen davranışlar

- JWT callback hata yollarındaki `NotImplementedException` kaldırıldı. Eksik kod
  her iki auth sağlayıcısında da kontrollü 400 yanıtı veriyor.
- Yönlendirme adresinin query ve fragment bölümleri hata mesajı eklenirken korunuyor.
- Tek request tipinin farklı response sözleşmeleri kullanması mediator'da cast
  hatasına yol açmıyor.
- Core ve YARP HTTP istemcileri çağıranın iptal isteğini normal hata yanıtına
  çevirmeden iletiyor.
- Gateway kimliğinde HTTP başlıklarının büyük/küçük harfi kimlik kaybına yol açmıyor.
  Gateway örneğinde dışarıdan gelen `Linbik-*` başlıklarının temizlenmesi etkinleştirildi.
- Android refresh isteği backend'in beklediği POST metodunu kullanıyor; logout GET kalıyor.
- CLI export işlemi diğer Linbik ayarlarını ve istemcilerini silmiyor; string enum
  ayarları okunabiliyor. Önceki CLI düzenlemesinde komut sonunda çökmeye neden olan
  tuş beklemesi de kaldırılmıştı.
- Nuxt geçersiz token için 401, eksik sunucu yapılandırması için 500 döndürüyor.
  Doğrulanmamış localStorage verisi kullanıcı bilgisi olarak kullanılmıyor.
- Arithmetic/Aggregation örneklerinin OpenAPI sürümleri Core ile uyumlu hale getirildi.

## Doğrulama

Yerel sonuç: .NET Release derlemesi 0 hata / 0 uyarı; 36 .NET testi,
3 Nuxt birim testi, 1 Nuxt HTTP smoke testi ve 3 Android testi başarılı.
Nuxt üretim paketi, Android kütüphanesi ve Android örnek uygulaması derlendi.

.NET kütüphaneleri, dört örnek proje ve test projesi kökteki `Linbik.slnx`
üzerinden birlikte derlenir:

```powershell
dotnet build Linbik.slnx -c Release
dotnet test tests/Linbik.Tests/Linbik.Tests.csproj -c Release --no-build --no-restore
```

Nuxt birim testleri ve derlenen uygulamanın HTTP kontrolü:

```powershell
cd examples/nuxt
npm ci
npm test
npm run build
npm run test:smoke
```

Android kütüphanesi, örnek uygulama ve HTTP sözleşmesi testleri:

```powershell
cd src/Android/Linbik.PasetoAuthManager.Android
./gradlew.bat :linbikauth:testDebugUnitTest :linbikauth:assembleDebug :sample:assembleDebug
```

Linux/macOS üzerinde aynı Gradle komutunu `bash gradlew` ile çalıştırın.
`.github/workflows/quality.yml` üç platform grubunu ayrı CI işlerinde çalıştırır.
Mevcut NuGet yayın workflow'u .NET 10'a ve gerçek test projesine güncellendi.
Workflow dosyaları yerel olarak düzenlendi; uzaktaki CI/yayın işlemleri tetiklenmedi.

## Kapsam ve sınırlar

Testler cookie politikalarını, callback hata yollarını, HTTP sözleşmelerini,
iptal davranışını, middleware temizliğini, actor dönüşümünü, mediator dispatch'ini,
generator çıktısının derlenmesini, configuration merge işlemini ve Nuxt callback
sayfasını kapsar. Üretim Linbik servisine karşı gerçek OAuth/PKCE akışı ve Android
Custom Tabs/deep-link yaşam döngüsü cihaz üzerinde bu çalışmada test edilmedi.

Nuxt örneğinin mevcut `session` cookie'si doğrulanmış claim'lerin görüntüleme
verisini taşır; tek başına bir yetkilendirme mekanizması değildir. Korunan servisler
imzalı token doğrulamasına ihtiyaç duyar.

Android'in mevcut AGP/Kotlin yapılandırmasında deprecation uyarıları ve Nuxt'ta
eski Browserslist verisi uyarısı vardır. Derlemeler geçmektedir; platform/paket
sürümü yükseltmeleri bu refactor'a eklenmemiştir.

Yeni kod için `.editorconfig` kullanılır. Mevcut sade dosyalarda yalnız değişiklik
üretmek amacıyla biçimsel yeniden yazım yapılmamıştır. Bu çalışma testlerle
korunan bir proje geneli refactor'dur; her olası çalışma senaryosunun doğrulandığı
veya bakım ihtiyacının sona erdiği anlamına gelmez.

## Yerel HTTP doğrulaması (10 Eylül 2026)

- Arithmetic toplama işlemi doğrudan ve Gateway üzerinden 200 döndürdü; 5 + 2 = 7 doğrulandı.
- Korunan çıkarma işlemi anonim istekte ve sahte Linbik kimlik başlıklarıyla 401 döndürdü.
- Gateway kimlik doğrulama uç noktaları için ortak istek sınırlama kurulumu eklendi. Güncellenmiş Gateway eksik kodlu callback isteğine 400 döndürdü.
- JWT ve PASETO callback testleri artık istek sınırının aşılmasında 429 yanıtını da doğruluyor. 36 .NET testi geçti.
- Yerel `.linbik/` ve tarayıcı oturum dosyaları Git ignore kurallarına eklendi. Önceden takip edilen iki servis kimlik dosyası yerelde korunarak Git takibinden çıkarıldı. Geçmiş commit’lerdeki kopyalar bu işlemle silinmez; yayımlanmış anahtarlar yenilenmelidir.
- Test hesabının giriş bilgileri hiçbir dosyaya yazılmadı. Test için başlatılan süreçler kapatıldı.
- Geçerli yerel HTTPS geliştirme sertifikası bulunamadığından gerçek hesapla OAuth giriş/yenileme ve Android cihaz akışı henüz doğrulanmadı.
