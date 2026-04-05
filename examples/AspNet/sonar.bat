cd /d "%~dp0"
dotnet sonarscanner begin /k:"linbik" /d:sonar.host.url="http://leptudo.com:9001"  /d:sonar.token="sqp_02ce82137ecfb4c6deec396ae30bb7291220b56f"

dotnet build

dotnet sonarscanner end /d:sonar.token="sqp_02ce82137ecfb4c6deec396ae30bb7291220b56f"

pause