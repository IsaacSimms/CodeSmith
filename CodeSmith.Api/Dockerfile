# == Multi-stage Dockerfile for CodeSmith.Api == #
# SDK stage for restore + publish (good layer cache on csproj/slnx).
# Final runtime stage is small aspnet image.
# Only CodeSmith.Core + Infrastructure + Api are intended to run in the container.
# Web, CLI and Tests are excluded via .dockerignore + publish target.

# == Build Stage == #
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files first for restore layer caching
COPY CodeSmith.slnx .
COPY CodeSmith.Core/CodeSmith.Core.csproj CodeSmith.Core/
COPY CodeSmith.Infrastructure/CodeSmith.Infrastructure.csproj CodeSmith.Infrastructure/
COPY CodeSmith.Api/CodeSmith.Api.csproj CodeSmith.Api/

# Restore the API project (and its dependencies)
RUN dotnet restore CodeSmith.Api/CodeSmith.Api.csproj

# Copy remaining source
COPY . .

# Publish only the API (transitive deps already restored)
RUN dotnet publish CodeSmith.Api/CodeSmith.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# == Runtime Stage == #
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Bring published output
COPY --from=build /app/publish .

# == EF Design-Time / DbContext Gotcha == #
# Microsoft.EntityFrameworkCore.Design is referenced only in CodeSmith.Infrastructure.csproj
# with PrivateAssets="all". There is no IDesignTimeDbContextFactory.
# In ServiceCollectionExtensions.cs (around lines 40-45) the DbContext is registered conditionally:
#   var conn = configuration.GetConnectionString("CodeSmithDb");
#   if (!string.IsNullOrWhiteSpace(conn)) opts.UseSqlServer(conn);
# Any future `dotnet ef migrations` or scaffolding that runs against/inside this image MUST
# have the "CodeSmithDb" connection string available (via environment, user secrets, or --connection).
# The container image itself does not contain the connection string.
# Migrations are intentionally not applied by this deploy workflow.

EXPOSE 8080
ENTRYPOINT ["dotnet", "CodeSmith.Api.dll"]
