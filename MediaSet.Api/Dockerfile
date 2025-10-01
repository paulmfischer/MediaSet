FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /build

# For use when deploying to a location
RUN git clone https://github.com/paulmfischer/MediaSet.git
WORKDIR /build/MediaSet/MediaSet.Api

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /mediaset
# For use when building somewhere else (not testing locally)
COPY --from=build-env /build/MediaSet/MediaSet.Api/out .
# For use when testing image creation locally
# COPY --from=build-env /build/out .
ENTRYPOINT ["dotnet", "MediaSet.Api.dll"]