# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Accept version from build args (passed by CI/CD)
# This will be used by MinVer as a fallback when git directory is not available
ARG MINVER_VERSION=0.0.0

# Copy global.json and solution file
COPY global.json ./
COPY MediaSet.sln ./

# Copy project files and restore dependencies
COPY MediaSet.Api.Analyzers/MediaSet.Api.Analyzers.csproj ./MediaSet.Api.Analyzers/
COPY MediaSet.Api/MediaSet.Api.csproj ./MediaSet.Api/
# Restore for linux-x64 only; this layer stays cached until the csproj files change
RUN dotnet restore MediaSet.Api/MediaSet.Api.csproj -r linux-x64

# Copy source code
COPY MediaSet.Api.Analyzers/ ./MediaSet.Api.Analyzers/
COPY MediaSet.Api/ ./MediaSet.Api/

# Copy .git directory so MinVer can access git metadata for version calculation.
# Kept after restore because .git changes on every commit and would invalidate the restore layer.
COPY .git/ .git/

# Build and publish for linux-x64 only (framework-dependent, matches the aspnet runtime image)
WORKDIR /src/MediaSet.Api
RUN dotnet publish -c Release -r linux-x64 --self-contained false --no-restore -o /app/publish /p:MinVerVersion=${MINVER_VERSION}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r mediaset && useradd -r -g mediaset mediaset

# Copy published output with the right ownership (avoids a duplicating chown layer)
COPY --from=build --chown=mediaset:mediaset /app/publish .

# Switch to non-root user
USER mediaset

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MediaSet.Api.dll"]