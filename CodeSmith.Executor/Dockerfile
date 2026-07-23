# == Multi-language CodeSmith Executor for ACA Dynamic Sessions == #
# Hyper-V isolation is provided by the session pool; this image only runs
# compilers/interpreters as ordinary subprocesses. Target port 8080.

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY CodeSmith.Executor/CodeSmith.Executor.csproj CodeSmith.Executor/
RUN dotnet restore CodeSmith.Executor/CodeSmith.Executor.csproj
COPY CodeSmith.Executor/ CodeSmith.Executor/
RUN dotnet publish CodeSmith.Executor/CodeSmith.Executor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image: aspnet + language toolchains (all 7 Tutoring languages)
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS final
WORKDIR /app

# Install system packages for Python, C++, Java, Node, Go, Rust tooling deps
RUN apt-get update && apt-get install -y --no-install-recommends \
        python3 \
        python3-pip \
        g++ \
        openjdk-17-jdk-headless \
        curl \
        ca-certificates \
        gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g tsx typescript \
    && curl -fsSL https://go.dev/dl/go1.22.5.linux-amd64.tar.gz | tar -C /usr/local -xz \
    && curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y --default-toolchain stable --profile minimal \
    && rm -rf /var/lib/apt/lists/*

ENV PATH="/usr/local/go/bin:/root/.cargo/bin:${PATH}"
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1
# Pre-populate NuGet template cache so first csharp `dotnet new` is faster
RUN dotnet new console -n Warmup -o /tmp/warmup --force && rm -rf /tmp/warmup

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CodeSmith.Executor.dll"]
