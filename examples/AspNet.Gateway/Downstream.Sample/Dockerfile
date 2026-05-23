# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/AspNet/Linbik.Core/Linbik.Core.csproj                          src/AspNet/Linbik.Core/
COPY examples/AspNet.Gateway/Downstream.Sample/Downstream.Sample.csproj examples/AspNet.Gateway/Downstream.Sample/
RUN dotnet restore examples/AspNet.Gateway/Downstream.Sample/Downstream.Sample.csproj

COPY src/AspNet/Linbik.Core/                       src/AspNet/Linbik.Core/
COPY examples/AspNet.Gateway/Downstream.Sample/    examples/AspNet.Gateway/Downstream.Sample/
RUN dotnet publish examples/AspNet.Gateway/Downstream.Sample/Downstream.Sample.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Downstream.Sample.dll"]
