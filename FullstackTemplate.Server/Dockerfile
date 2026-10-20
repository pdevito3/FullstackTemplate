# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Build.props .editorconfig ./
COPY FullstackTemplate.Server/*.csproj FullstackTemplate.Server/
COPY FullstackTemplate.SourceGenerators/*.csproj FullstackTemplate.SourceGenerators/
RUN dotnet restore FullstackTemplate.Server/FullstackTemplate.Server.csproj

COPY FullstackTemplate.SourceGenerators/ FullstackTemplate.SourceGenerators/
COPY FullstackTemplate.Server/ FullstackTemplate.Server/
WORKDIR /src/FullstackTemplate.Server
RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Kestrel:Endpoints in appsettings.json binds 8080 (HTTP/1.1) and 8081 (HTTP/2 only, h2c, for gRPC).
# The base image sets ASPNETCORE_HTTP_PORTS=8080. Kestrel ignores it and logs a warning, so clear it.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=

EXPOSE 8080 8081

# The aspnet image has no curl, so the image has no HEALTHCHECK. Northflank probes /health itself.

ENTRYPOINT ["dotnet", "FullstackTemplate.Server.dll"]
