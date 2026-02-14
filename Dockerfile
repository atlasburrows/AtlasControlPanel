# Atlas Control Panel — Docker Image
# Uses SQLite by default for zero-config deployment
#
# Build:  docker build -t atlas-control-panel .
# Run:    docker run -d -p 5263:5263 -v atlas-data:/app/data atlas-control-panel
#
# Environment variables:
#   AUTH_USERNAME    - Login username (default: admin)
#   AUTH_PASSWORD    - Login password (default: changeme)
#   AUTH_DISPLAYNAME - Dashboard display name (default: Admin)
#   API_KEY          - API key for plugin auth (default: empty = no auth)
#   CONNECTION_STRING - Override DB connection (default: SQLite in /app/data)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files
COPY src/Atlas.Domain/Atlas.Domain.csproj Atlas.Domain/
COPY src/Atlas.Application/Atlas.Application.csproj Atlas.Application/
COPY src/Atlas.Infrastructure/Atlas.Infrastructure.csproj Atlas.Infrastructure/
COPY src/Atlas.Shared/Atlas.Shared.csproj Atlas.Shared/
COPY src/Atlas.Web/Atlas.Web.csproj Atlas.Web/
RUN dotnet restore Atlas.Web/Atlas.Web.csproj

# Copy all source
COPY src/ .
RUN dotnet publish Atlas.Web/Atlas.Web.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Create data directory for SQLite
RUN mkdir -p /app/data

# Default environment
ENV ASPNETCORE_URLS=http://+:5263
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/atlas.db"
ENV Auth__Username=admin
ENV Auth__Password=changeme
ENV Auth__DisplayName=Admin
ENV Api__Key=""

EXPOSE 5263
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Atlas.Web.dll"]
