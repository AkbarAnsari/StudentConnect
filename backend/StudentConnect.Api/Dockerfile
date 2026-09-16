# Runtime-only image: expects `dotnet publish StudentConnect.Api.csproj -c Release -o
# ../publish` to have already been run on the host (see the root README Docker section) —
# NuGet restore doesn't happen inside this build because this network's proxy blocks
# nuget.org from containers. Build context is the "backend/" folder, one level up from
# this Dockerfile — e.g. docker build -f StudentConnect.Api/Dockerfile -t studentconnect-api .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY publish/ .

# Program.cs resolves "database/schema.sql"/"seed.sql" as ../database relative to
# ContentRootPath (WORKDIR /app here), so the scripts need to land at /database for the
# same auto-apply-on-startup logic used in local dev to work unmodified in the container.
COPY database /database

# 8080 is a common convention for containerized ASP.NET Core apps (avoids binding a
# privileged port and matches what Azure App Service for Containers / Container Apps expect).
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "StudentConnect.Api.dll"]
