# NuGet paketinin kaynak kodunda debug

`publish.bat` varsayılan olarak Release yayınlar. Optimizasyonsuz bir paket
yayınlamak için `publish.bat Debug` çalıştırın. Bu komut normal yayın akışını
(sürüm güncelleme ve NuGet'e yükleme dahil) Debug yapılandırmasıyla çalıştırır.
Paketleme hatasında yayın durur.

Yayınlamadan yerel paket oluşturmak için:

```powershell
dotnet pack src/AspNet/Linbik.YARP/Linbik.YARP.csproj -c Debug -o ./nupkg /p:PackageVersion=0.0.0-debug.1
```

Tüketen projede yeni paket sürümünü seçin. Tüketen projeyi Debug derlemek,
önceden Release derlenmiş NuGet kütüphanesinin optimizasyonlarını kapatmaz.
Linbik çalışma zamanı projeleri sembolleri ve kaynakları DLL içine gömer;
ayrı bir sembol sunucusu gerekmez.

Visual Studio'da Tools > Options > Debugging > General bölümündeki
**Enable Just My Code** seçeneğini kapatın. Debug sırasında Modules penceresinde
yüklenen Linbik DLL yolunu/sürümünü ve sembol durumunu kontrol edin, ardından
F11 ile çağrıya girin. Gerekirse Modules > Extract Source Code ile gömülü
kaynağı açın. Yerel çalışma dosyasındaki breakpoint'in bağlanması için dosyanın
pakette derlenen kaynakla eşleşmesi gerekir.

Microsoft belgeleri:
- https://learn.microsoft.com/en-us/visualstudio/debugger/just-my-code
- https://learn.microsoft.com/en-us/visualstudio/debugger/decompilation

# Application istemcisi ve OpenAPI 401

OpenAPI isteği 401 dönerse istemci dosyası değiştirilmez. Önceden üretilmiş ve
uygulamaya derlenmiş `Linbik.YARP.Generated` istemcisinin somut sınıfı ve
arayüzü DI'a kaydedilir. Endpoint iki türden birini parametre olarak kullanabilir.

Üretim başlangıçta kaynak dosyasını diske yazar; çalışan uygulamaya yeni tür
yüklemez. İlk başarılı üretimden sonra yeniden derleyin. 401 yanıtının kendisi
bu DI düzeltmesiyle çözülmez; OpenAPI adresinin erişim/kimlik doğrulama ayarları
ayrıca doğrulanmalıdır. YARP belge isteği 401 alırsa ilgili IntegrationServices
anahtarını kullanarak Linbik Application token'ını alır ve aynı belge adresine
Bearer token ile yalnızca bir kez tekrar istek gönderir. Anonim belge erişimi
token gerektirmez. Token alınamaz veya tekrar reddedilirse mevcut dosya korunur.
Özel namespace veya farklı isimdeki istemciler açıkça
DI'a kaydedilmelidir.
