# Linbik.PasetoAuthManager.Android

A native library that lets Android apps whose backend uses **Linbik.PasetoAuthManager**
(ASP.NET) sign users in with their Linbik account, plus a sample app that uses this
library.

```
Linbik.PasetoAuthManager.Android/
  linbikauth/   # Publishable Android library (com.linbik.pasetoauth)
  sample/       # Minimal example app using the library
```

## This is DIFFERENT from the web/redirect version of "Sign in with Linbik"

This library does not target the **302 redirect** flow that `Linbik.JwtAuthManager`/
`Linbik.PasetoAuthManager` use for web clients — it targets the **mobile client** flow
marked with `ActionResultType: "Json"`. Token exchange is held on **your** backend (as a
PASETO cookie) — the app opens Linbik's authorization/consent page in **Custom Tabs**
(per RFC 8252, not a WebView); once the user approves, Linbik redirects to a deep link
specific to your app (`{applicationId}://oauth/callback?code=...`). The app takes this
code and forwards it to **your own** backend's `/api/Linbik/callback` endpoint to
complete the session.

## Backend Configuration (required prerequisite)

Your backend's `appsettings.json` must have a client under `Linbik:Clients` marked with
`ActionResultType: "Json"` and a `Name` field set. `GetClientConfig` looks **only** at the
`Name` field — there is **no** `ClientType` field (it is ignored even if present):

```jsonc
{
  "Linbik": {
    // ...
    "Clients": [
      {
        "Name": "Mobile",
        "ClientId": "...",
        "ActionResultType": "Json"
      }
    ]
  }
}
```

Do not try to use this library with `ActionResultType: "Redirect"` (the default/web
clients) — `/api/Linbik/login` returns an HTML/redirect page, not JSON, and the library
cannot parse it (it fails with a clear error message instead).

**If you're using KeylessMode** (the default — a single `ClientId` auto-provisioned via
`.linbik/credentials.json`): you do **not** need to separately register a "mobile client"
on the Linbik server. Each entry in the `Clients` list only determines whether **your own
backend** returns Json or Redirect for a given `name` request — it does not mean a
separate app/redirect URI registration on Linbik's side. So reusing the same `ClientId`
across both a `"Name": "Default"` (web, Redirect) entry and a `"Name": "Mobile"` (Json)
entry is **completely normal and the intended usage** — you are not asked to enter a
separate URL/redirect URI in the dashboard, because there is no dashboard registration yet
(provisioning happens automatically from the backend's own HTTP request).

If `clientName` is left blank (or the `"Name"` field is omitted entirely, defaulting to
`"Default"`), the backend uses the **first** entry in the `Clients` list in KeylessMode —
if that first entry isn't `ActionResultType: Json` (e.g. it's a web client), the mobile
flow won't work. For this reason it's recommended to add a separate, explicitly named
entry for mobile (e.g. `"Name": "Mobile"`) and give the same name to
`LinbikPasetoAuthOptions.clientName`.

**When creating a Client for this app in Linbik.App (or Linbik.Api)**, set the
`RedirectUri` field to the custom URI scheme based on your app's `applicationId`:

```
{applicationId}://oauth/callback
```

E.g. for the `sample` module: `com.linbik.pasetoauth.sample://oauth/callback`. This is
the address Linbik redirects the browser to after receiving the code (`code`) — it is
**not** your backend's own callback URL (see "How It Works" below). In your own app,
[`linbikauth`'s manifest](linbikauth/src/main/AndroidManifest.xml) generates this scheme
automatically via the `${applicationId}` placeholder; no extra manifest changes are
needed. Copy the created Client's `clientId` and paste it into the `ClientId` field of
the `"Mobile"` entry in `appsettings.json`.

## How It Works

1. The library uses its own OkHttp client (in the background, with no UI) to request
   `{backendBaseUrl}/api/Linbik/login?name=Mobile`. The backend returns the PKCE
   `code_verifier` as a `Set-Cookie` and returns Linbik's actual login/consent page
   address (`redirectPath`) in the JSON body.
2. This address is opened in a **Custom Tabs** tab (NOT in a WebView — per RFC 8252;
   also, only Custom Tabs/an external browser loaded via App Links can redirect properly
   to the Linbik.Mobil app — a WebView does not support this).
3. The user signs in/approves on Linbik. Linbik redirects the browser to the
   `RedirectUri` registered for this app in Linbik.App, i.e.
   `{applicationId}://oauth/callback?code=...`.
4. This custom URI scheme is caught by
   [`LinbikRedirectActivity`](linbikauth/src/main/kotlin/com/linbik/pasetoauth/LinbikRedirectActivity.kt)
   and forwarded to the already-open `LinbikAuthActivity` (`onNewIntent`). The `code`
   query parameter is extracted here.
5. The library requests your backend's `{backendBaseUrl}/api/Linbik/callback?code=...`
   endpoint (again with its own OkHttp client) using this same `code`. The backend
   performs PKCE verification using the cookie it wrote in step 1, completes the token
   exchange, and returns the user info (`LoginCallbackResponse`) as JSON.
6. The result is returned to your app. The session cookies the backend wrote via
   `Set-Cookie` (including `HttpOnly` cookies like `authToken`, `linbik_refresh`) are
   held in [`LinbikSharedCookieJar`](linbikauth/src/main/kotlin/com/linbik/pasetoauth/LinbikSharedCookieJar.kt),
   a persistent OkHttp `CookieJar` implementation — it is not tied to any WebView, it is
   shared by the library's own OkHttp client.

Because steps 1 and 5 use the **same** `LinbikSharedCookieJar` instance, the PKCE
`code_verifier` cookie is preserved between them and the backend's
`PkceService.GetVerifier(...)` verification works normally. Custom Tabs' own cookies
(belonging to Linbik's login page) are completely separate from these and are never seen
or used by your app.

## Installation

### 1. Add via JitPack

Add the JitPack repository to your project's `settings.gradle.kts`:

```kotlin
dependencyResolutionManagement {
    repositories {
        google()
        mavenCentral()
        maven { url = uri("https://jitpack.io") }
    }
}
```

Then add the dependency to your app's `build.gradle.kts`:

```kotlin
dependencies {
    implementation("com.github.tepecam18.Linbik:linbikauth:1.2.4")
}
```

## Usage

The example below shows the basic sign-in flow. For more detailed technical information
and advanced usage (refresh token, cookie management, etc.), see
[linbikauth/README.md](linbikauth/README.md).

```kotlin
class MyActivity : ComponentActivity() {
    private val authClient = LinbikPasetoAuthClient()
    private lateinit var launcher: ActivityResultLauncher<LinbikPasetoAuthOptions>

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        // 1. Register the launcher (must be inside onCreate!)
        launcher = authClient.registerLauncher(this) { result ->
            when (result) {
                is LinbikAuthResult.Success -> {
                    // User signed in successfully: result.displayName, result.userId, etc.
                }
                is LinbikAuthResult.Error -> { /* Error message: result.message */ }
                LinbikAuthResult.Cancelled -> { /* User cancelled */ }
            }
        }

        signInButton.setOnClickListener {
            // 2. Start the flow
            launcher.launch(
                LinbikPasetoAuthOptions(
                    backendBaseUrl = "https://your-backend.com",
                    clientName = "Mobile"
                )
            )
        }
    }
}
```

## Distribution — Google Play Closed Testing

The sample app is published on Google Play as a closed test. To install it:

1. Join the tester group first: https://groups.google.com/g/linbik
2. Then install from the Play Store listing: https://play.google.com/store/apps/details?id=com.linbik

> Note: the published Play Store listing's application id is referenced above as
> `com.linbik`; this has not been independently cross-checked against this repo's sample
> app applicationId (`com.linbik.pasetoauth.sample`), so treat the Play Store listing as
> the source of truth for the actual published package.

## Technical Details and Development

For the library's internal structure, Custom Tabs integration, and a contribution guide,
see the [README](linbikauth/README.md) in the library directory.
