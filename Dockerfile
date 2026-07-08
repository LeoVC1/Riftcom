# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first for better layer caching
COPY RiftboundStore/RiftboundStore.csproj RiftboundStore/
RUN dotnet restore RiftboundStore/RiftboundStore.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish RiftboundStore/RiftboundStore.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# SQLite database lives on a persistent volume mounted at /data.
# The connection string can be overridden by env var, but we set a sensible default.
ENV ConnectionStrings__DefaultConnection="DataSource=/data/app.db;Cache=Shared"

# Kestrel binds to this port; Fly.io routes public HTTPS traffic here as HTTP.
ENV ASPNETCORE_URLS="http://+:8080"
ENV ASPNETCORE_ENVIRONMENT="Production"

EXPOSE 8080

ENTRYPOINT ["dotnet", "RiftboundStore.dll"]
