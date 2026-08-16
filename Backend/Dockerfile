# Use the official .NET 8 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
EXPOSE 8080
EXPOSE 8081

# Use the official .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FlowServiceBackend.csproj", "."]
RUN dotnet restore "./FlowServiceBackend.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./FlowServiceBackend.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FlowServiceBackend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage/image
FROM base AS final
WORKDIR /app

# Create directory for data protection keys
USER root
RUN mkdir -p /app/keys && chown app:app /app/keys
USER app

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyApi.dll"]

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:${PORT:-8080}/health || exit 1
