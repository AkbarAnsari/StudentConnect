# Multi-stage build: restore/build/publish with the full SDK, then run the published
# output in the smaller ASP.NET runtime image (the runtime image doesn't need the SDK).
# Build context is the "backend/" folder (one level up from this Dockerfile) since the
# Api project now references sibling Domain/Application/Infrastructure projects —
# e.g. docker build -f StudentConnect.Api/Dockerfile -t studentconnect-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StudentConnect.Domain/*.csproj StudentConnect.Domain/
COPY StudentConnect.Application/*.csproj StudentConnect.Application/
COPY StudentConnect.Infrastructure/*.csproj StudentConnect.Infrastructure/
COPY StudentConnect.Api/*.csproj StudentConnect.Api/
RUN dotnet restore StudentConnect.Api/StudentConnect.Api.csproj

COPY . .
RUN dotnet publish StudentConnect.Api/StudentConnect.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 8080 is a common convention for containerized ASP.NET Core apps (avoids binding a
# privileged port and matches what Azure App Service for Containers / Container Apps expect).
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "StudentConnect.Api.dll"]
