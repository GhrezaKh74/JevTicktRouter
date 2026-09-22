# syntax=docker/dockerfile:1

# ------------------------------------------------------------------------------------------------
# JevTicketRouter — one image that serves both halves.
#
# The React app is built to static files and copied into the API's wwwroot, so the whole thing runs
# from a single origin on one port: no CORS, no reverse proxy, and the client's relative /api calls
# work untouched.
#
#   docker build -t jevticketrouter .
#   docker run --rm -p 8080:8080 jevticketrouter
#
# With no API key it starts in Mock mode, so it is useful immediately.
# ------------------------------------------------------------------------------------------------


# --- Stage 1: build the React app ---------------------------------------------------------------
FROM node:22-alpine AS frontend

WORKDIR /src/frontend

# Copy the manifests alone first. This layer is only invalidated when dependencies actually change,
# so editing application code does not trigger a fresh npm install.
COPY frontend/jev-ticket-router-web/package.json frontend/jev-ticket-router-web/package-lock.json ./
RUN npm ci

COPY frontend/jev-ticket-router-web/ ./

# VITE_API_BASE_URL is deliberately left empty: the API is served from this same origin, so the
# client calls /api relatively.
RUN npm run build


# --- Stage 2: build and publish the API ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend

WORKDIR /src

# Same trick for NuGet: restore against the project files alone so a code edit does not re-restore.
COPY backend/Directory.Build.props ./backend/
COPY backend/JevTicketRouter.Domain/JevTicketRouter.Domain.csproj ./backend/JevTicketRouter.Domain/
COPY backend/JevTicketRouter.Application/JevTicketRouter.Application.csproj ./backend/JevTicketRouter.Application/
COPY backend/JevTicketRouter.Infrastructure/JevTicketRouter.Infrastructure.csproj ./backend/JevTicketRouter.Infrastructure/
COPY backend/JevTicketRouter.Api/JevTicketRouter.Api.csproj ./backend/JevTicketRouter.Api/
RUN dotnet restore backend/JevTicketRouter.Api/JevTicketRouter.Api.csproj

COPY backend/ ./backend/

# The test project is not copied, so it is not built here. Tests run in CI or locally, not as part
# of producing a runtime image.
RUN dotnet publish backend/JevTicketRouter.Api/JevTicketRouter.Api.csproj \
        --no-restore \
        --configuration Release \
        --output /app/publish


# --- Stage 3: runtime ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# curl is only here to give the container a real health check.
RUN apk add --no-cache curl

WORKDIR /app

COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot

# Run as a non-root user. The aspnet images ship an `app` user (uid 1654) for exactly this.
RUN chown -R app:app /app
USER app

# Kestrel binds to all interfaces on 8080. 8080 rather than 80 so the non-root user can bind it.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

EXPOSE 8080

# /api/health reports the active AI provider, which makes it a meaningful readiness signal rather
# than just "the process is up".
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "JevTicketRouter.Api.dll"]
