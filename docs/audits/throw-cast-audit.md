# Throw ve type cast denetimi

Tarih: 6 Kasım 2026. Bu kayıt yayımlanmış paketleri değil, bu denetim sırasındaki kaynak ağacını anlatır. Satırlar ilerideki düzenlemelerde değişebilir.

## Kapsam ve yöntem

- C# için regex yerine Roslyn syntax ağacı kullanıldı: gerçek cast ifadeleri, `as`, `throw`, yeniden fırlatma ve `ThrowIf*` guard çağrıları ayrıldı. Yorumlar, normal parantezler, `bin` ve `obj` envantere alınmadı.
- Web/Node kaynaklarındaki açık `throw` noktaları ve Android Kotlin kaynaklarındaki `throw`/`as` kullanımları ayrıca incelendi. Bu Kotlin kaynaklarında `as` cast bulunmadı.
- Örnek uygulamalar, test kodu ve bağımlılıkların içindeki exception noktaları kapsam dışıdır. Bu bir implicit conversion, bütün olası null erişimleri veya bütün runtime exception yolları için biçimsel güvenlik kanıtı değildir.

## Sonuç

C# envanteri: **64 açık throw**, **13 yeniden fırlatma**, **71 guard çağrısı**, **20 doğrudan cast** ve **10 `as` ifadesi**; toplam 46 dosya. Web/Node tarafında 59, Android tarafında 7 açık throw satırı incelendi.

## Düzeltilen sorunlar

| Alan | Sorun ve sonuç |
|---|---|
| CLI appsettings | Yanlış tipteki Linbik/Clients bölümleri sessizce değiştirilmek yerine dosya ve alan adıyla reddediliyor. Geçersiz kök, ClientId ve URL alanlarında dosya değişmeden kalıyor. |
| OpenAPI birleştirme | Host paths/components/categories ve downstream version/reference/discriminator alanları, ham InvalidCastException veya NullReferenceException yerine alanı açıklayan sözleşme hataları veriyor. Başarısız birleştirme giriş belgelerini değiştirmiyor. |
| Sender / actor | Eksik handler hatası gerçek request/response tipini ve AddLinbikSlices/AddScoped düzeltmesini gösteriyor. Handler oluşturulurken çıkan özgün hatalar yanlışlıkla eksik kayıt diye sarılmıyor. Null provider/actor açık argüman hatası veriyor. |
| Standalone state | Linbik'in application property anahtarındaki yanlış tip, ham cast hatası yerine hangi anahtarın kütüphaneye ait olduğunu söylüyor. |
| PASETO ömrü | Süresi geçmiş veya int dakika aralığına sığmayan expiry reddediliyor. Büyük double-to-int dönüşümünün yanlışlıkla bir dakikalık token üretmesi önlendi. |
| Retry-After | Pozitif kesirli saniye yukarı yuvarlanıyor; negatif/çok büyük değerler güvenli int aralığına sınırlandırılıyor. |
| Apps refresh | NaN, sonsuz, sıfır, negatif veya yüzde yerine 75 yazılması açık ayar hatası veriyor. Timer aralığı da desteklenen aralıkta doğrulanıyor; doğrudan Options.Create ile kurulumda da korunuyor. |
| Key ring / refresh store | Eksik/tekrarlanan KeyId ve geçersiz overlap ayarları alan adıyla açıklanıyor. İptal edilmiş key rotation mutasyondan önce duruyor. Süresi geçmiş refresh session ve eksik hash için açık guard eklendi. |
| Kayıt mesajları | Server/YARP için IServiceCollection üzerinde olmayan çağrı önerileri, AddLinbik(...).AddLinbikServer/AddLinbikYarp zinciriyle düzeltildi. Eksik service URL ve validation-key mesajları gereken ayarı belirtiyor. |
| Web/Node kurulum | Yanlış URL, UUID, API key tipi, PASERK tipi, fetch/navigate/cookie callback ve cookie header tipleri erken reddediliyor. Hata mesajları girilen gizli değeri tekrar etmiyor. |
| HTTP geliştirme seçeneği | allowInsecureHttp için string "false" gibi truthy değerler reddediliyor; boolean isteniyor. |
| MemorySessionStore | Geçersiz kapasite ve sonlanmayan/yanlış tip expiry değerleri reddediliyor. Kapasite dolduğunda uygulanabilir çözüm gösteriliyor. |
| Android | Boş sunucu mesajı kullanıcıya boş hata olarak gösterilmiyor. JSON parse hatasının cause bilgisi korunuyor; cancellation yeniden fırlatılmaya devam ediyor. |

## Güvenli olduğu için korunan kullanımlar

- `throw;` ve Kotlin cancellation yeniden fırlatmaları: stack/cancellation semantiğini koruyor. Bunlar genel bir exception ile değiştirilmedi.
- Roslyn callback ve code-fix cast'leri: kayıtlı SymbolKind veya mevcut syntax parent türü tarafından garanti ediliyor. SyntaxNode'a üst tipe dönüşüm güvenli.
- Sender wrapper ve TRequest cast'leri: wrapper hem gerçek request tipi hem response tipiyle anahtarlanıyor; iki farklı response sözleşmesini destekleyen test korunuyor.
- JsonObject/JsonArray DeepClone cast'leri: önceden bilinen somut JSON türü clone sırasında değişmiyor. Downstream paths cast'leri aynı metottaki şekil kontrolünden sonra yapılıyor.
- TCP listener LocalEndpoint cast'i: IPAddress.Loopback ile oluşturulan TcpListener IPEndPoint üretiyor.
- HTTP status enum dönüşümleri ve heartbeat uptime dönüşümü: hedef sayısal aralığa sığıyor.
- Nullable actor interface `as` işlemleri: yanlış flow'da null dönmek sözleşmenin parçası. Zorunlu actor DI kayıtları bunu açıklayıcı bir hata ile ayırıyor.
- Token/cache/import sınırlarındaki authentication ve sözleşme hataları kaldırılmadı; başarısız istekler public veya başarılı sonuca çevrilmedi.

## Doğrulama

- `dotnet test tests/Linbik.Tests/Linbik.Tests.csproj --no-restore`: **308 test geçti**.
- `node --test src/Web/Linbik.PasetoAuthManager.Web/test/*.test.js src/Web/Linbik.PasetoAuthManager.Server/test/*.test.js`: **42 test geçti**.
- Android `:linbikauth:testDebugUnitTest`: **başarılı**. İlk offline denemede bağımlılıklar eksikti; online bağımlılık çözümlemesinden sonra Kotlin derlemesi ve JVM testleri tamamlandı. Cihaz/emülatör testi yapılmadı.
- Hatalı JSON ve ayar şekillerinin dosya/belgeyi değiştirmediği, hata mesajlarının örnek gizli değerleri taşımadığı, handler exception'larının korunduğu ve sayısal sınırlar regresyon testleriyle kontrol ediliyor.
- Değişiklikler paket yayımlamaz. Eski paket sürümleri bu kontrolleri otomatik edinmez.

## Dosya bazında C# envanteri

`THROW`: yeni exception; `RETHROW`: özgün exception; `GUARD`: ThrowIf çağrısı; `CAST`: doğrudan dönüşüm; `AS`: nullable tip dönüşümü. Listeler kaynak satır numaralarıdır.

| Dosya | Noktalar |
|---|---|
| `src/AspNet/AuthManager.Shared/RefreshTokens.cs` | GUARD: 40, 41, 42, 51, 71; THROW: 44 |
| `src/AspNet/Linbik.CLI/Services/AppSettingsManager.cs` | THROW: 70, 78, 84, 118, 133, 143 |
| `src/AspNet/Linbik.CLI/Services/LinbikApiClient.cs` | CAST: 143 |
| `src/AspNet/Linbik.Core/Builders/LinbikBuilder.cs` | THROW: 8 |
| `src/AspNet/Linbik.Core/Configuration/LinbikOptionsValidator.cs` | GUARD: 19 |
| `src/AspNet/Linbik.Core/Extensions/LinbikApplicationBuilderExtensions.cs` | THROW: 57 |
| `src/AspNet/Linbik.Core/Extensions/LinbikFlowDocumentExtensions.cs` | GUARD: 20 |
| `src/AspNet/Linbik.Core/Extensions/LinbikOpenApiExtensions.cs` | GUARD: 20 |
| `src/AspNet/Linbik.Core/Extensions/LinbikRateLimitingExtensions.cs` | CAST: 128; GUARD: 42, 58 |
| `src/AspNet/Linbik.Core/Extensions/LinbikServiceCollectionExtensions.cs` | GUARD: 34, 50; THROW: 205 |
| `src/AspNet/Linbik.Core/Extensions/LinbikStandaloneExtensions.cs` | AS: 95; GUARD: 49; THROW: 38, 65, 87, 98, 130, 136, 151 |
| `src/AspNet/Linbik.Core/Extensions/PasetoBearerExtensions.cs` | GUARD: 24, 25, 26 |
| `src/AspNet/Linbik.Core/Identity/LActor.cs` | AS: 122, 123, 124; GUARD: 59, 102; THROW: 108, 110 |
| `src/AspNet/Linbik.Core/Services/LinbikAuthClient.cs` | RETHROW: 77, 82, 142, 147, 214, 219, 293, 298, 363 |
| `src/AspNet/Linbik.Core/Services/LinbikAuthService.cs` | GUARD: 75, 121, 176; THROW: 36, 37, 38, 40, 41 |
| `src/AspNet/Linbik.Core/Services/LinbikHeartbeatService.cs` | CAST: 93 |
| `src/AspNet/Linbik.Core/Services/LinbikProvisionClient.cs` | CAST: 151; THROW: 206, 214 |
| `src/AspNet/Linbik.Core/Services/PasetoHelperService.cs` | GUARD: 26, 27, 28, 65, 66, 67, 101, 168, 169, 170, 205, 206, 207, 236, 237 |
| `src/AspNet/Linbik.Core/Services/PasetoKeyRing.cs` | GUARD: 22, 70, 71, 72; THROW: 25, 27, 29, 44 |
| `src/AspNet/Linbik.JwtAuthManager/Extensions/LinbikJwtAuthExtensions.cs` | GUARD: 57 |
| `src/AspNet/Linbik.PasetoAuthManager/Extensions/LinbikPasetoAuthExtensions.cs` | GUARD: 51 |
| `src/AspNet/Linbik.PasetoAuthManager/Services/LocalPasetoTokenIssuer.cs` | CAST: 46 |
| `src/AspNet/Linbik.Server/Configuration/ServerOptionsValidator.cs` | GUARD: 14 |
| `src/AspNet/Linbik.Server/Extensions/ServerExtensions.cs` | GUARD: 45; THROW: 136 |
| `src/AspNet/Linbik.Server/Services/IntegrationTokenValidator.cs` | GUARD: 30, 31 |
| `src/AspNet/Linbik.Slices.CodeFixes/LinbikSliceCodeFixProvider.cs` | CAST: 152, 153 |
| `src/AspNet/Linbik.Slices.Generators/LinbikSliceAnalyzer.cs` | AS: 78; CAST: 30 |
| `src/AspNet/Linbik.Slices.Generators/LinbikSliceGenerator.cs` | AS: 44, 56 |
| `src/AspNet/Linbik.Slices.Testing/LinbikSliceTestingExtensions.cs` | GUARD: 34, 59, 72, 73, 88; THROW: 76 |
| `src/AspNet/Linbik.Slices/Abstractions/ILinbikValidator.cs` | THROW: 23 |
| `src/AspNet/Linbik.Slices/Endpoints/LinbikEndpoint.cs` | GUARD: 22 |
| `src/AspNet/Linbik.Slices/Endpoints/LinbikSliceStartupValidator.cs` | GUARD: 27; THROW: 54 |
| `src/AspNet/Linbik.Slices/Extensions/LinbikSlicesServiceCollectionExtensions.cs` | AS: 43, 46; GUARD: 26, 60, 72; THROW: 44, 47 |
| `src/AspNet/Linbik.Slices/Pipeline/LinbikSender.cs` | CAST: 28, 48; GUARD: 21; THROW: 15, 63 |
| `src/AspNet/Linbik.Slices/Results/LValidation.cs` | GUARD: 30 |
| `src/AspNet/Linbik.YARP/Configuration/YARPOptionsValidator.cs` | GUARD: 13 |
| `src/AspNet/Linbik.YARP/Extensions/LinbikYarpExtensions.cs` | CAST: 432, 587; GUARD: 36, 79; THROW: 42, 649 |
| `src/AspNet/Linbik.YARP/Extensions/YARPManagerExtensions.cs` | GUARD: 43 |
| `src/AspNet/Linbik.YARP/OpenApi/DelegatedDocumentCache.cs` | AS: 114; RETHROW: 93; THROW: 72, 114 |
| `src/AspNet/Linbik.YARP/OpenApi/DelegatedDocumentMerger.cs` | CAST: 23, 24, 42, 81; GUARD: 13, 14, 15, 16, 17; THROW: 20, 22, 34, 56, 73, 84, 86, 107, 119, 128 |
| `src/AspNet/Linbik.YARP/OpenApi/DelegatedDocumentReader.cs` | CAST: 47; THROW: 18 |
| `src/AspNet/Linbik.YARP/Services/ApplicationClientGenerationHostedService.cs` | CAST: 91; RETHROW: 54; THROW: 170 |
| `src/AspNet/Linbik.YARP/Services/ApplicationPasetoAuthHandler.cs` | THROW: 33 |
| `src/AspNet/Linbik.YARP/Services/ApplicationServiceClient.cs` | CAST: 298; RETHROW: 184, 227; THROW: 23, 365 |
| `src/AspNet/Linbik.YARP/Services/ApplicationTokenProvider.cs` | CAST: 43; GUARD: 52, 53, 54, 55; THROW: 64, 66, 73 |
| `src/AspNet/Linbik.YARP/Services/MultiJwtTokenProvider.cs` | THROW: 38, 66, 93, 115 |

## Web/Android throw envanteri

| Dosya | Satırlar |
|---|---|
| `src/Android/Linbik.PasetoAuthManager.Android/linbikauth/src/main/kotlin/com/linbik/pasetoauth/LinbikAuthActivity.kt` | 146, 240 |
| `src/Android/Linbik.PasetoAuthManager.Android/linbikauth/src/main/kotlin/com/linbik/pasetoauth/LinbikAuthHttpClient.kt` | 27, 43, 47, 53, 62 |
| `src/Web/Linbik.PasetoAuthManager.Server/src/index.js` | 23, 26, 30, 36, 38, 45, 48, 49, 50, 51, 52, 54, 62, 63, 66, 75, 76, 78, 79, 84, 85, 87, 91, 97, 104, 108, 109, 139, 142, 151, 173, 174, 186, 192, 194, 207 |
| `src/Web/Linbik.PasetoAuthManager.Server/src/store.js` | 6, 15, 17 |
| `src/Web/Linbik.PasetoAuthManager.Web/src/index.js` | 21, 24, 26, 27, 29, 36, 40, 47, 58, 73, 77, 99, 117 |
| `src/Web/Linbik.PasetoAuthManager.Web/src/server.js` | 5, 8, 10, 11, 12, 14, 30 |

Android JVM test raporları: 5 test, 0 hata/başarısızlık.
