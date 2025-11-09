# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Accept version from build args (passed by CI/CD)
# This will be used by MinVer as a fallback when git directory is not available
ARG MINVER_VERSION=0.0.0

# Copy global.json and solution file
COPY global.json ./
COPY MediaSet.sln ./

# Copy .git directory so MinVer can access git metadata for version calculation
COPY .git/ .git/

# Copy project file and restore dependencies
COPY MediaSet.Api/MediaSet.Api.csproj ./MediaSet.Api/
RUN dotnet restore MediaSet.Api/MediaSet.Api.csproj

# Copy source code
COPY MediaSet.Api/ ./MediaSet.Api/

# Build and publish
WORKDIR /src/MediaSet.Api
RUN dotnet publish -c Release -o /app/publish --no-restore /p:MinVerVersion=${MINVER_VERSION}

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r mediaset && useradd -r -g mediaset mediaset

# Copy published output
COPY --from=build /app/publish .

# Set ownership
RUN chown -R mediaset:mediaset /app

# Switch to non-root user
USER mediaset

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MediaSet.Api.dll"]