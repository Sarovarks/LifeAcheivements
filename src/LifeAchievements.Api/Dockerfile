# Build from the repository root:
#   docker build -f src/LifeAchievements.Api/Dockerfile -t lifeachievements-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so a code-only change does not re-run restore.
COPY LifeAchievements.slnx ./
COPY src/LifeAchievements.Domain/*.csproj          src/LifeAchievements.Domain/
COPY src/LifeAchievements.Application/*.csproj     src/LifeAchievements.Application/
COPY src/LifeAchievements.Infrastructure/*.csproj  src/LifeAchievements.Infrastructure/
COPY src/LifeAchievements.Shared/*.csproj          src/LifeAchievements.Shared/
COPY src/LifeAchievements.Api/*.csproj             src/LifeAchievements.Api/
RUN dotnet restore src/LifeAchievements.Api/LifeAchievements.Api.csproj

COPY src/ src/
RUN dotnet publish src/LifeAchievements.Api/LifeAchievements.Api.csproj \
    -c Release -o /app --no-restore

# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# The aspnet image is Debian-based and ships no HTTP client at all, so a container
# healthcheck has nothing to call the app with. curl is the smallest way to fix that.
# Azure's probes reach the app over the network and do not need this, but compose's
# depends_on: service_healthy does.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# The aspnet image ships a non-root "app" user; use it rather than running as root.
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

COPY --from=build --chown=app:app /app ./

# 127.0.0.1 rather than localhost: localhost resolves to ::1 first, and Kestrel is
# bound to IPv4 here, so the probe would be refused by the wrong address family.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=5 \
    CMD curl -fsS http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "LifeAchievements.Api.dll"]
