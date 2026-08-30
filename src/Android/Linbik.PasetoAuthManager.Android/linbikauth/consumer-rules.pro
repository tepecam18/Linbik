# Linbik Auth Library ProGuard Rules

# Preserve the Activity results and options classes
-keep class com.linbik.pasetoauth.LinbikAuthResult { *; }
-keep class com.linbik.pasetoauth.LinbikAuthResult$* { *; }
-keep class com.linbik.pasetoauth.LinbikPasetoAuthOptions { *; }

# Preserve Activities and internal logic used by the library
-keep class com.linbik.pasetoauth.LinbikAuthActivity { *; }
-keep class com.linbik.pasetoauth.LinbikRedirectActivity { *; }

# Keep OkHttp and JSON related metadata if necessary (usually handled by their own consumer rules)
-keepattributes Signature, InnerClasses, EnclosingMethod
