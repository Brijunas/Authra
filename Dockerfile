# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore (layer caching)
COPY src/Authra.Domain/Authra.Domain.csproj src/Authra.Domain/
COPY src/Authra.Application/Authra.Application.csproj src/Authra.Application/
COPY src/Authra.Infrastructure/Authra.Infrastructure.csproj src/Authra.Infrastructure/
COPY src/Authra.Api/Authra.Api.csproj src/Authra.Api/
RUN dotnet restore src/Authra.Api/Authra.Api.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/Authra.Api/Authra.Api.csproj -c Release -o /app/publish --no-restore

# Runtime: Ubuntu Chiseled (no shell, non-root, minimal attack surface)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
USER app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Authra.Api.dll"]
