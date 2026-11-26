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

# The aspnet image ships a non-root "app" user; use it rather than running as root.
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 8080

COPY --from=build --chown=app:app /app ./

# No in-container HEALTHCHECK on purpose. This image carries no HTTP client, and installing
# one made every build depend on a Debian mirror being reachable - which broke CI once with
# the same Dockerfile that had passed minutes earlier. Both hosts probe /health/live over the
# network from outside the container (Render's healthCheckPath, the probes in
# infra/main.bicep), so nothing actually needed a probe in here.

ENTRYPOINT ["dotnet", "LifeAchievements.Api.dll"]
