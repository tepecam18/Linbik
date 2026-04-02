@echo off
setlocal enabledelayedexpansion

:: =======================================
:: AYARLAR DOSYASI YAPILANDIRMASI
:: =======================================
set SETTINGS_FILE=publish-settings.txt
set NUGET_SOURCE=https://api.nuget.org/v3/index.json

:: Eger ayar dosyasi yoksa, ornek bir tane olusturalim
if not exist "%SETTINGS_FILE%" (
    echo VERSION=1.0.0> "%SETTINGS_FILE%"
    echo SUFFIX=>> "%SETTINGS_FILE%"
    echo API_KEY=Sizin_Nuget_Api_Keyiniz_Buraya_Gelecek>> "%SETTINGS_FILE%"
    echo ===========================================
    echo [%SETTINGS_FILE%] isimli ayar dosyasi olusturuldu. 
    echo Lutfen bu dosyayi acip icerisindeki API_KEY degerini guncelleyin.
    echo Eger "-preview.1" gibi bir ekleme yapmak isterseniz SUFFIX kismina yazabilirsiniz.
    echo Ardindan bu .bat dosyasini tekrar calistirin.
    echo ===========================================
    pause
    goto :eof
)

:: Degiskenleri baslangicta bosaltalim
set CURRENT_VERSION=
set SUFFIX=
set API_KEY=

:: Dosyadan ayarlari okuyalim
for /f "tokens=1,* delims==" %%a in (%SETTINGS_FILE%) do (
    if /i "%%a"=="VERSION" set CURRENT_VERSION=%%b
    if /i "%%a"=="SUFFIX" set SUFFIX=%%b
    if /i "%%a"=="API_KEY" set API_KEY=%%b
)

:: Bosluklari temizleyelim (sürüm için)
set CURRENT_VERSION=%CURRENT_VERSION: =%

:: Versiyonu noktadan (x.y.z) bolelim
for /f "tokens=1,2,3 delims=." %%a in ("%CURRENT_VERSION%") do (
    set MAJOR=%%a
    set MINOR=%%b
    set PATCH=%%c
)

:: Eger format x.y.z degilse veya bos ise varsayilan deger atayalim
if "%MAJOR%"=="" set MAJOR=1
if "%MINOR%"=="" set MINOR=0
if "%PATCH%"=="" set PATCH=0

:: ===========================================
:: SECIM MENUSU
:: ===========================================
cls
echo ===========================================
echo YAYINLANACAK PAKETI SECIN
echo ===========================================
echo [1] Hepsi (Tum Paketler)
echo [2] Sadece Linbik.Core
echo [3] Sadece Linbik.JwtAuthManager
echo [4] Sadece Linbik.YARP
echo [5] Sadece Linbik.Server
echo [6] Sadece Linbik.Cli
echo ===========================================
set TARGET=1
set /p TARGET="Seciminiz (1-6) [Varsayilan 1]: "

set PACK_CORE=0
set PACK_JWT=0
set PACK_YARP=0
set PACK_SERVER=0
set PACK_CLI=0

if "%TARGET%"=="1" (
    set PACK_CORE=1
    set PACK_JWT=1
    set PACK_YARP=1
    set PACK_SERVER=1
    set PACK_CLI=1
)
if "%TARGET%"=="2" ( set PACK_CORE=1 )
if "%TARGET%"=="3" ( set PACK_JWT=1 )
if "%TARGET%"=="4" ( set PACK_YARP=1 )
if "%TARGET%"=="5" ( set PACK_SERVER=1 )
if "%TARGET%"=="6" ( set PACK_CLI=1 )

:: Eger SUFFIX yoksa Patch (son rakam) degerini bir arttiralim
:: SUFFIX varsa ana versiyona dokunmuyoruz, manuel takip edilmesi daha guvenlidir.
if "%SUFFIX%"=="" (
    set /a PATCH+=1
)
set NEW_VERSION=%MAJOR%.%MINOR%.%PATCH%

:: Tam versiyonu olustur (varsa Suffix ekle)
set FULL_VERSION=%NEW_VERSION%%SUFFIX%

:: Dosyayi yeni versiyonla guncelleyelim
> "%SETTINGS_FILE%" echo VERSION=%NEW_VERSION%
>> "%SETTINGS_FILE%" echo SUFFIX=%SUFFIX%
>> "%SETTINGS_FILE%" echo API_KEY=%API_KEY%

echo.
echo ===========================================
echo Baslangic Versiyonu : %CURRENT_VERSION%%SUFFIX%
echo Yeni Versiyon       : %FULL_VERSION%
echo Ayarlar             : %SETTINGS_FILE% dosyasindan alindi.
echo ===========================================
echo.

echo [1/5] Projeler Restore Ediliyor...
dotnet restore ./examples/AspNet/AspNet.sln
if %ERRORLEVEL% neq 0 ( echo Restore hatasi! & pause & exit /b %ERRORLEVEL% )

echo.
echo [2/5] Projeler Build Ediliyor...
dotnet build ./examples/AspNet/AspNet.sln --configuration Release --no-restore
if %ERRORLEVEL% neq 0 ( echo Build hatasi! & pause & exit /b %ERRORLEVEL% )

echo.
echo [3/5] Testler Calistiriliyor...
dotnet test ./examples/AspNet/AspNet.sln --no-restore --verbosity normal
if %ERRORLEVEL% neq 0 ( echo Test hatasi! & pause & exit /b %ERRORLEVEL% )

echo.
echo [4/5] Nuget Paketleri Olusturuluyor (Versiyon: %FULL_VERSION%)...
if exist .\nupkg rmdir /s /q .\nupkg

if "%PACK_CORE%"=="1" (
    echo - Linbik.Core paketleniyor...
    dotnet pack ./src/AspNet/Linbik.Core/Linbik.Core.csproj -c Release -o .\nupkg /p:PackageVersion=%FULL_VERSION%
)
if "%PACK_JWT%"=="1" (
    echo - Linbik.JwtAuthManager paketleniyor...
    dotnet pack ./src/AspNet/Linbik.JwtAuthManager/Linbik.JwtAuthManager.csproj -c Release -o .\nupkg /p:PackageVersion=%FULL_VERSION%
)
if "%PACK_YARP%"=="1" (
    echo - Linbik.YARP paketleniyor...
    dotnet pack ./src/AspNet/Linbik.YARP/Linbik.YARP.csproj -c Release -o .\nupkg /p:PackageVersion=%FULL_VERSION%
)
if "%PACK_SERVER%"=="1" (
    echo - Linbik.Server paketleniyor...
    dotnet pack ./src/AspNet/Linbik.Server/Linbik.Server.csproj -c Release -o .\nupkg /p:PackageVersion=%FULL_VERSION%
)
if "%PACK_CLI%"=="1" (
    echo - Linbik.Cli paketleniyor...
    dotnet pack ./src/AspNet/Linbik.Cli/Linbik.Cli.csproj -c Release -o .\nupkg /p:PackageVersion=%FULL_VERSION%
)

echo.
echo [5/5] Paketler Nuget'e Yayinlanacak...
if "%API_KEY%"=="Sizin_Nuget_Api_Keyiniz_Buraya_Gelecek" (
    echo [UYARI] API_KEY ayarlanmamis! 
    echo Lutfen %SETTINGS_FILE% dosyasini acarak API_KEY degerini kendi key'iniz ile degistirin.
    pause
    goto :eof
)
if "%API_KEY%"=="" (
    echo [UYARI] API_KEY bos! 
    echo Lutfen %SETTINGS_FILE% dosyasini acarak API_KEY degerini doldurun.
    pause
    goto :eof
)

if not exist .\nupkg\*.nupkg (
    echo [HATA] Yayinlanacak paket secilmedi veya olusturulamadi!
    pause
    goto :eof
)

:: Dosya bulanamadi hatasini cozmek icin nupkg klasorundeki tum paketleri tek tek pushluyoruz.
for %%f in (.\nupkg\*.nupkg) do (
    echo Yayinlaniyor: %%f
    dotnet nuget push "%%f" --api-key %API_KEY% --source %NUGET_SOURCE% --skip-duplicate
    if !ERRORLEVEL! neq 0 ( echo Publish hatasi: %%f & pause & exit /b !ERRORLEVEL! )
)

echo ===========================================
echo TUM ISLEMLER BASARIYLA TAMAMLANDI!
echo ===========================================
pause
