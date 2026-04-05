dotnet pack -c Release
dotnet tool uninstall --global Linbik.Cli
dotnet tool install --global Linbik.Cli --add-source ./bin/Release/
pause