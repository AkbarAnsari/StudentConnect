# Multi-stage build: restore/build/publish with the full SDK, then run the published
# output in the smaller ASP.NET runtime image (the runtime image doesn't need the SDK).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY StudentConnect.Api.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 8080 is a common convention for containerized ASP.NET Core apps (avoids binding a
# privileged port and matches what Azure App Service for Containers / Container Apps expect).
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "StudentConnect.Api.dll"]
