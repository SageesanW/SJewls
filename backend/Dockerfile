# ==============================================================================
# SJewls .NET 8 Backend - Multi-Stage Dockerfile for Railway Deployment
# Target Framework: .NET 8.0 (net8.0)
# Build Context: /backend (set Railway Root Directory to /backend)
# ==============================================================================

# ------------------------------------------------------------------------------
# Stage 1: Build & Publish with .NET 8 SDK
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to maximize Docker layer caching during package restore
COPY src/SJewls.Domain/SJewls.Domain.csproj src/SJewls.Domain/
COPY src/SJewls.Application/SJewls.Application.csproj src/SJewls.Application/
COPY src/SJewls.Infrastructure/SJewls.Infrastructure.csproj src/SJewls.Infrastructure/
COPY src/SJewls.Api/SJewls.Api.csproj src/SJewls.Api/

# Restore NuGet dependencies for SJewls.Api and all referenced projects
RUN dotnet restore src/SJewls.Api/SJewls.Api.csproj

# Copy the full application source code
COPY src/ src/

# Build and publish SJewls.Api in Release mode
RUN dotnet publish src/SJewls.Api/SJewls.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ------------------------------------------------------------------------------
# Stage 2: Runtime with ASP.NET Core 8
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Production environment defaults
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0
ENV PORT=8080

# Copy compiled application binaries
COPY --from=build /app/publish .

# Informational port exposure (Railway dynamically maps the PORT environment variable)
EXPOSE 8080

# Start the SJewls API
ENTRYPOINT ["dotnet", "SJewls.Api.dll"]
