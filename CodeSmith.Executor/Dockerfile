# == Multi-language CodeSmith Executor == #
# Runs student code as ordinary subprocesses. Deployed as a scale-to-zero Azure
# Container App on internal ingress (also usable by the Dynamic Sessions Adapter).
# ACA is a shared kernel, not a microVM, so this image runs as a non-root user and
# the container itself is the only isolation boundary. Target port 8080.

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src
COPY CodeSmith.Executor/CodeSmith.Executor.csproj CodeSmith.Executor/
RUN dotnet restore CodeSmith.Executor/CodeSmith.Executor.csproj
COPY CodeSmith.Executor/ CodeSmith.Executor/
RUN dotnet publish CodeSmith.Executor/CodeSmith.Executor.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image: aspnet + language toolchains (all 7 Tutoring languages)
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS final
WORKDIR /app

# Rust lives outside /root so a non-root runtime user can reach the toolchain.
ENV RUSTUP_HOME=/opt/rust/rustup
ENV CARGO_HOME=/opt/rust/cargo

# Toolchains install in separate layers so one flaky download does not invalidate the others.
# Every fetch retries: these are large transfers and a truncated stream piped straight into
# tar/sh fails the whole build (curl 18 / "Unexpected EOF in archive").
ARG CURL_RETRY="--retry 5 --retry-delay 3 --retry-all-errors --fail --silent --show-error --location"

# Python, C++, Java, Node + TypeScript tooling
RUN apt-get update && apt-get install -y --no-install-recommends \
        python3 \
        python3-pip \
        g++ \
        openjdk-17-jdk-headless \
        curl \
        ca-certificates \
        gnupg \
    && curl $CURL_RETRY https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g tsx typescript \
    && npm cache clean --force \
    && rm -rf /var/lib/apt/lists/*

# Go — download to disk and verify before extracting, so a partial transfer fails loudly
RUN curl $CURL_RETRY -o /tmp/go.tar.gz https://go.dev/dl/go1.22.5.linux-amd64.tar.gz \
    && gzip -t /tmp/go.tar.gz \
    && tar -C /usr/local -xzf /tmp/go.tar.gz \
    && rm /tmp/go.tar.gz \
    && /usr/local/go/bin/go version

# Rust — installed under /opt/rust (see RUSTUP_HOME/CARGO_HOME) so the non-root user can reach it
RUN curl $CURL_RETRY --proto '=https' --tlsv1.2 https://sh.rustup.rs -o /tmp/rustup.sh \
    && sh /tmp/rustup.sh -y --default-toolchain stable --profile minimal \
    && rm /tmp/rustup.sh \
    && chmod -R a+rX /opt/rust \
    && /opt/rust/cargo/bin/rustc --version

ENV PATH="/usr/local/go/bin:/opt/rust/cargo/bin:${PATH}"
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_NOLOGO=1

# == Non-root runtime user == #
# Student code inherits this identity, so it must not be able to write the app or toolchains.
RUN useradd --create-home --uid 10001 --shell /usr/sbin/nologin executor
ENV HOME=/home/executor
# Toolchain scratch dirs must resolve under the executor's writable home, not /root
ENV DOTNET_CLI_HOME=/home/executor
ENV NUGET_PACKAGES=/home/executor/.nuget/packages
ENV GOCACHE=/home/executor/.cache/go-build
ENV GOPATH=/home/executor/go
ENV NPM_CONFIG_CACHE=/home/executor/.npm

COPY --from=build /app/publish .

# Warm the template/package caches AS the runtime user so the first csharp run can use them.
USER executor
RUN dotnet new console -n Warmup -o /tmp/warmup --force && rm -rf /tmp/warmup

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CodeSmith.Executor.dll"]
