# Blazor WebAssembly publishes to plain static files, so the runtime image is just nginx.
# Build from the repository root:
#   docker build -f src/LifeAchievements.Web/Dockerfile -t lifeachievements-web .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LifeAchievements.slnx ./
COPY src/LifeAchievements.Shared/*.csproj src/LifeAchievements.Shared/
COPY src/LifeAchievements.Web/*.csproj    src/LifeAchievements.Web/
RUN dotnet restore src/LifeAchievements.Web/LifeAchievements.Web.csproj

COPY src/LifeAchievements.Shared/ src/LifeAchievements.Shared/
COPY src/LifeAchievements.Web/    src/LifeAchievements.Web/
RUN dotnet publish src/LifeAchievements.Web/LifeAchievements.Web.csproj \
    -c Release -o /app --no-restore

# ---------------------------------------------------------------------------

FROM nginx:1.27-alpine AS runtime

COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY src/LifeAchievements.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/LifeAchievements.Web/docker-entrypoint.sh /docker-entrypoint.d/40-api-base-url.sh

RUN chmod +x /docker-entrypoint.d/40-api-base-url.sh

EXPOSE 8080

# nginx:alpine already has an entrypoint that runs everything in /docker-entrypoint.d,
# so the API URL script above runs before the server starts.
