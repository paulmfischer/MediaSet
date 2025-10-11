FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /build

# Disable .NET diagnostics to prevent socket creation issues in containers
ENV DOTNET_EnableDiagnostics=0

# For use when deploying to a location
RUN git clone https://github.com/paulmfischer/MediaSet.git
WORKDIR /build/MediaSet

# Remove the global.json from the cloned repo to avoid version conflicts
RUN rm -f global.json

# Copy everything
COPY . ./MediaSet.Api/

WORKDIR /build/MediaSet/MediaSet.Api
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out
# Clean up any diagnostic sockets or temp files that might cause issues
RUN find /tmp -name "dotnet-diagnostic-*" -type s -delete 2>/dev/null || true

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /mediaset
# Copy the published output
COPY --from=build-env /build/MediaSet/MediaSet.Api/out .
ENTRYPOINT ["dotnet", "MediaSet.Api.dll"]