# syntax=docker/dockerfile:1.7
# Multi-stage build — Linbik kökü (../../) context olarak kullanılır.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj'ları önce kopyala (restore cache'i için)
COPY src/AspNet/Linbik.Core/Linbik.Core.csproj                                  src/AspNet/Linbik.Core/
COPY examples/AspNet.Gateway/Linbik.Gateway.Sample/Linbik.Gateway.Sample.csproj examples/AspNet.Gateway/Linbik.Gateway.Sample/
RUN dotnet restore examples/AspNet.Gateway/Linbik.Gateway.Sample/Linbik.Gateway.Sample.csproj

# Kaynağı kopyala ve publish et
COPY src/AspNet/Linbik.Core/                              src/AspNet/Linbik.Core/
COPY examples/AspNet.Gateway/Linbik.Gateway.Sample/       examples/AspNet.Gateway/Linbik.Gateway.Sample/
RUN dotnet publish examples/AspNet.Gateway/Linbik.Gateway.Sample/Linbik.Gateway.Sample.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Docker \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Non-root user (image'da hazır: app)
USER $APP_UID

ENTRYPOINT ["dotnet", "Linbik.Gateway.Sample.dll"]
