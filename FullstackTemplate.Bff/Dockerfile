# Build frontend
FROM node:22-alpine AS frontend
WORKDIR /app
RUN corepack enable && corepack prepare pnpm@latest --activate
COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ .
RUN pnpm build

# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Packages.props .
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

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "FullstackTemplate.Bff.dll"]
