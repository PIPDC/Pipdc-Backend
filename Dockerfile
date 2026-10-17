# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against the project file so this layer is cached until the package
# graph changes. Directory.Build.props must be present too: it supplies
# TargetFramework/Nullable/ImplicitUsings to the csproj.
COPY Directory.Build.props ./
COPY src/PIPDC.csproj src/
RUN dotnet restore src/PIPDC.csproj

COPY . .
RUN dotnet publish src/PIPDC.csproj -c Release -o /app/out --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Render provides the listening port in $PORT; fall back to 8080 for local runs.
CMD ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet PIPDC.dll"]
