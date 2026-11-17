# Süresi dolan yetkilendirme işlemleri

Linbik API/arayüzü (`ptts`) ve SDK'lar birlikte güncellenmelidir. Önce SDK kullanan
uygulamalara hata callback desteğini dağıtın, ardından Linbik API/arayüzünü yayınlayın.
Eski SDK'lar `error` dönüşünü eksik authorization code hatası olarak değerlendirebilir.

- Başlangıç tokenı tek kullanımlık ve 10 dakika geçerlidir.
- Profil/onay işlemi 5 dakika geçerlidir; süre uzatılmaz.
- API başlangıç tokenıyla ilişkili client/state bilgisini 30 dakika saklar. Bu bilgi
  yalnızca kayıtlı callback'e `error=authorization_expired` ile dönmeye yarar.
- Yenilemede tüketilmiş token veya profil/onay isteğinde süresi dolmuş session,
  arayüzde işlemi bitirir ve uygulamaya dönme bağlantısı gösterir.
- Bilinmeyen token, başka kullanıcıya bağlı işlem veya kapatılmış client/servis için
  dış yönlendirme üretilmez. Referrer veya tarayıcıdan gelen return URL kullanılmaz.
- API'nin recovery kaydı mevcut IMemoryCache üzerinde tutulur; süreç yeniden
  başladığında veya 30 dakika sonunda hedef bulunamazsa güvenli yerel ekran gösterilir.
  Birden fazla instance için başlangıç ve recovery kayıtları aynı paylaşımlı depoya taşınmalıdır.

ASP.NET JWT/PASETO SDK'ları başlangıçta korumalı, HttpOnly/Secure/SameSite=Lax bir
recovery çerezi oluşturur; `state` eşleşen hata callback'inde bu çerezi ve PKCE
çerezini temizler. Kullanıcı client'ın yapılandırılmış `RedirectUrl` adresine
`error` mesajı ve `error_code=authorization_expired` ile gönderilir. Bu adres herkese
açık bir giriş/karşılama sayfası olmalı; mesajı ve yeniden giriş düğmesini uygulama
sunmalıdır. Başarısız deneme mevcut kullanıcı oturumunu kapatmaz. Data Protection
anahtarlarını üretimde kalıcı tutun ve aynı uygulamanın instance'ları arasında paylaşın.

Node SDK adaptörleri `callbackError(error, state)` kullanmalıdır; Nuxt FullStack
örneği ana sayfaya kontrollü dönüşü gösterir. Android SDK zaten custom scheme
üzerindeki `error`/`error_description` dönüşlerini kullanıcıya hata olarak aktarır.

Doğrulama: API'de authorize öncesi expiry, tüketilmiş token, profil/onay expiry,
kullanıcı bağı, bilinmeyen token ve kapalı client; .NET'te her iki sağlayıcının hata
callback'i ve yanlış state; Node'da expiry sonrası dönüş, replay, yanlış state,
yeni giriş ve recovery kaydının başarılı callback süresini uzatmaması test edilir.
