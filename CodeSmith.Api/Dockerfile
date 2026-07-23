# == Multi-stage Dockerfile for CodeSmith.Api == #
# Build stage uses sdk:9.0 so we can parse/restore the modern CodeSmith.slnx solution format
# (requires .NET SDK 9.0.200+). The final runtime stage stays on aspnet:8.0 to match the
# application's TargetFramework and keep the deployed image small/stable.
# Only CodeSmith.Core + Infrastructure + Api ship in the image.
# Web, CLI and Tests are excluded via .dockerignore + targeted publish.

# == Build Stage == #
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution + all project manifests first for restore layer caching.
# We must include manifests for every <Project> listed in CodeSmith.slnx (including the
# excluded ones) so the slnx parser succeeds during `dotnet restore CodeSmith.slnx`.
# The matching negations in .dockerignore ensure only the tiny files are re-included
# while the heavy source directories for Web/CLI/Tests stay out of the build context.
COPY CodeSmith.slnx .
COPY CodeSmith.Core/CodeSmith.Core.csproj CodeSmith.Core/
COPY CodeSmith.Infrastructure/CodeSmith.Infrastructure.csproj CodeSmith.Infrastructure/
COPY CodeSmith.Api/CodeSmith.Api.csproj CodeSmith.Api/
COPY CodeSmith.CLI/CodeSmith.CLI.csproj CodeSmith.CLI/
COPY CodeSmith.Tests/CodeSmith.Tests.csproj CodeSmith.Tests/
COPY CodeSmith.Executor/CodeSmith.Executor.csproj CodeSmith.Executor/
COPY CodeSmith.Web/CodeSmith.Web.esproj CodeSmith.Web/

# Restore using the solution file. This is the single source of truth and matches the
# commands used by developers (`dotnet build CodeSmith.slnx`, `dotnet test CodeSmith.slnx`).
RUN dotnet restore CodeSmith.slnx

# Copy remaining source
COPY . .

# Publish only the API (and its transitive dependencies). We target the concrete csproj
# for publish scope even though restore used the slnx; this is explicit and minimal.
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
