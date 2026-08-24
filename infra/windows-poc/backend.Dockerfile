# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0.408-bookworm-slim AS build

ARG PROJECT_PATH
RUN test -n "$PROJECT_PATH"

WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT_PATH" \
    && dotnet publish "$PROJECT_PATH" \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0.15-bookworm-slim AS runtime

ARG APP_DLL
RUN test -n "$APP_DLL" \
    && apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    APP_DLL=${APP_DLL}

WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080

USER app
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
