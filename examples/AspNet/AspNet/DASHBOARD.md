# 🎨 Dashboard Test Guide

## Proper MVC Pattern ile Dashboard

AspNet.Examples artık **MVC (Model-View-Controller)** pattern kullanıyor!

### 📁 Dosya Yapısı
```
Controllers/
  └─ TestController.cs         ← Controller (25 satır)
Models/
  └─ DashboardViewModel.cs     ← ViewModel
Views/
  └─ Test/
      └─ Index.cshtml           ← Razor View (275 satır)
```

### 🔗 URL
```
http://localhost:7020/test
```

## 🎯 Dashboard Özellikleri

### Giriş Yapmadıysa
- ❌ "Kullanıcı Giriş Yapmamış" durumu
- 🔵 **"Linbik ile Giriş Yap"** butonu
- Tıklayınca → `/linbik/login` → Linbik.App'e redirect

### Giriş Yaptıysa
- ✅ "Kullanıcı Giriş Yapmış" durumu
- 👤 **Kullanıcı Bilgileri**:
  - User ID
  - Username  
  - Nickname
- 🔑 **Integration Tokens** (varsa):
  - Package name
  - Token length
  - Expiration time
- 🔴 **"Çıkış Yap"** butonu

## 🚀 Kullanım

```powershell
# 1. Uygulamayı çalıştır
dotnet run

# 2. Tarayıcıda aç
Start http://localhost:7020/test

# 3. "Linbik ile Giriş Yap" butonuna tıkla

# 4. Linbik.App'te giriş yap

# 5. Dashboard'a geri dön ve bilgileri gör
```

## 🏗️ MVC Pattern

### Controller (Clean & Simple)

```csharp
public class TestController(IAuthService authService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var profile = await authService.GetUserProfileAsync(HttpContext);
        var tokens = await authService.GetIntegrationTokensAsync(HttpContext);
        
        var model = new DashboardViewModel
        {
            IsLoggedIn = profile != null,
            Profile = profile,
            Tokens = tokens ?? new List<IntegrationToken>()
        };
        
        return View(model);
    }
}
```

### ViewModel (Type-Safe)

```csharp
public class DashboardViewModel
{
    public bool IsLoggedIn { get; set; }
    public UserProfile? Profile { get; set; }
    public List<IntegrationToken> Tokens { get; set; } = new();
}
```

### View (Razor Syntax)

```html
@model AspNet.Models.DashboardViewModel

<div class="status @(Model.IsLoggedIn ? "logged-in" : "logged-out")">
    @if (Model.IsLoggedIn)
    {
        <h3>Hoş geldiniz, @Model.Profile!.UserName!</h3>
        @foreach (var token in Model.Tokens)
        {
            <li>📦 @token.PackageName</li>
        }
    }
</div>
```

## 🔄 Akış

```
1. /test (dashboard)
   ↓
2. TestController.Index() → Model oluştur
   ↓
3. View(model) → Razor render
   ↓
4. HTML döndür
```

## 📝 Avantajlar

### HTML String Döndürme (Eski Yöntem)
```csharp
❌ private string GenerateDashboardHtml(...) { return $@"<html>..."; }
❌ 250+ satır HTML string concatenation
❌ Syntax highlighting yok
❌ IntelliSense desteği yok
❌ Type-safety yok
```

### Razor View (Yeni Yöntem)
```csharp
✅ return View(model);
✅ Syntax highlighting
✅ IntelliSense desteği
✅ Type-safe model binding
✅ Partial view desteği
✅ Layout/Section desteği
✅ Tag helpers
```

## 📊 Dosya Boyutları

| Dosya | Satır | Açıklama |
|-------|-------|----------|
| `TestController.cs` | 25 | Clean controller |
| `DashboardViewModel.cs` | 9 | Simple model |
| `Index.cshtml` | 275 | Full Razor view |

**Toplam**: 309 satır (önceden: 278 satır controller'da)

## 🎨 Tasarım

- **Gradient Background**: Purple to violet
- **Card Layout**: Modern, shadowed cards
- **Responsive Grid**: Auto-fit minmax(250px, 1fr)
- **Smooth Transitions**: Hover effects
- **Badge System**: Success/warning colors
- **Empty States**: Friendly messages

## 🔌 Otomatik Endpoint'ler

Linbik kütüphaneleri bu endpoint'leri otomatik oluşturur:

- `GET /linbik/login` - Linbik'e redirect
- `GET /linbik/callback` - Token exchange
- `GET /linbik/logout` - Session temizle
- `GET /test` - Dashboard (manuel MVC controller)

---

**✨ MVC Pattern - Clean, Maintainable, Professional!**
