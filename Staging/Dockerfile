# Build context: repository root containing api/ and database/ (from branch: backend)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file first for layer caching
COPY api/src/Lumi.Api/Lumi.Api.csproj api/src/Lumi.Api/
RUN dotnet restore api/src/Lumi.Api/Lumi.Api.csproj

# Copy full API source and publish
COPY api/ api/
RUN dotnet publish api/src/Lumi.Api/Lumi.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for container health checks
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /app/logs

ENV ASPNETCORE_ENVIRONMENT=Staging \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=15s --retries=4 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Lumi.Api.dll"]
