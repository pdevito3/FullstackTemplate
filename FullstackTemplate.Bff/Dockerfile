# Build frontend
FROM node:22-alpine AS frontend
WORKDIR /app
# Corepack uses the pnpm version of packageManager in package.json
RUN corepack enable
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ .
RUN pnpm build

# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props Directory.Build.props .editorconfig ./
COPY FullstackTemplate.Bff/*.csproj FullstackTemplate.Bff/
RUN dotnet restore FullstackTemplate.Bff/FullstackTemplate.Bff.csproj

COPY FullstackTemplate.Bff/ FullstackTemplate.Bff/
WORKDIR /src/FullstackTemplate.Bff
RUN dotnet publish -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .
COPY --from=frontend /app/dist ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# The aspnet image has no curl, so the image has no HEALTHCHECK. The host probes /health itself.

ENTRYPOINT ["dotnet", "FullstackTemplate.Bff.dll"]
