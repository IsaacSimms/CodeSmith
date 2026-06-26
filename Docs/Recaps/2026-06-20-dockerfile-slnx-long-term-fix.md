# Dockerfile .slnx Long-Term Fix

**Date:** 2026-06-20
**Type:** fix
**Environment / Systems:** .NET 8, Docker multi-stage build, GitHub Actions (deploy-azure.yml), Windows + PowerShell

## TL;DR

Replaced the temporary workaround (`dotnet restore CodeSmith.Api/CodeSmith.Api.csproj`) with proper long-term support for the project's `CodeSmith.slnx` solution file. Build stage updated to `mcr.microsoft.com/dotnet/sdk:9.0` while keeping the final image on `aspnet:8.0`. Added COPY instructions for the three excluded project manifests (CLI, Tests, Web .esproj) so `dotnet restore CodeSmith.slnx` succeeds in the thin layer. Comments updated to explain the SDK split and restore choice. Decision reached via grill-me Q&A; EF design-time block preserved exactly.

## Context & Goal

The Dockerfile originally intended to use `CodeSmith.slnx` (per the azure-deploy-seam design and .dockerignore negations) but failed at build time with `error MSB4068: The element <Solution> is unrecognized`. A short-term direct-csproj restore was applied to unblock deploys. The user supplied a detailed handoff document asking for a clean, maintainable long-term fix that keeps the runtime on .NET 8, restores using the authoritative solution file, and follows .NET container best practices. Goal: eliminate the divergence so container builds match local dev commands and documented intent.

## Key Points Explored

- Root cause: .NET 8 SDK (used in build stage) does not support the modern .slnx XML format introduced for SDK 9.0.200+ / VS 17.12+.
- Original design (2026-06-19 azure deploy seam): full `CodeSmith.slnx` restore + .dockerignore `!` patterns for the tiny manifests of excluded projects (Web.esproj, CLI, Tests) so restore layer stays small.
- Trade-offs: direct csproj restore works for the small Api→Core→Infra graph but abandons the single-source-of-truth decision and leaves dead COPY + outdated comments.
- Option A (handoff preference): newer SDK only in build stage + `dotnet restore CodeSmith.slnx`. Runtime untouched.
- .esproj test: `dotnet restore CodeSmith.slnx` succeeded cleanly on a machine with SDK 10.0 (includes the JavaScript.Sdk project).
- To preserve layer caching, the three additional manifest COPY lines must be present before the restore RUN (they were already accounted for in .dockerignore).
- No changes allowed to target framework, final image, workflow, or application behavior.
- EF comment in Dockerfile (conditional DbContext + Design package PrivateAssets) remains relevant and was left verbatim.

## Decisions & Outcomes

- Through ask_user_question grilling, user explicitly selected "Implement slnx + sdk:9.0 (recommended best practice for consistency)".
- Performed minimal targeted edits to `CodeSmith.Api/Dockerfile`:
  - `FROM .../sdk:8.0` → `.../sdk:9.0`
  - Added three manifest COPY lines for CLI, Tests, and Web.esproj
  - `RUN dotnet restore CodeSmith.Api/CodeSmith.Api.csproj` → `RUN dotnet restore CodeSmith.slnx`
  - Rewrote opening block and relevant comments to document why the build stage uses a newer SDK and that slnx is now the active target.
- `dotnet restore CodeSmith.slnx` verified locally (succeeded with up-to-date output).
- Docker daemon unavailable in agent environment, so full `docker build` verification delegated to user.
- All other content (especially the large EF Design-Time / DbContext Gotcha section) untouched.
- Result: Dockerfile, .dockerignore comments, and project build commands are now consistent again.

## Open Questions / Next Steps

- Run `docker build -f CodeSmith.Api/Dockerfile -t codesmith-api:verify .` locally (or via Docker Desktop) to confirm success.
- After local verification, merge and manually trigger the "Deploy Azure" workflow to validate the image builds and deploys in ACR + Container Apps.
- Monitor for any future solution-graph additions that would require updating the manifest COPY list alongside .dockerignore.

## Artifacts

- `CodeSmith.Api/Dockerfile` (updated with slnx restore)
- `.dockerignore` (no changes required; its slnx comments are now accurate again)
- `CodeSmith.slnx` (now actually exercised by the container build path)
- Git diff of the session (three focused search/replaces)
- This recap: `Recaps/2026-06-20-dockerfile-slnx-long-term-fix.md`
- Source handoff document (pasted by user at thread start)
- Prior related: `Handoffs/2026-06-19-azure-deploy-seam-handoff.md` and `Recaps/2026-06-19-azure-deploy-seam.md` (original slnx restore intent)
- Verification command (from handoff): `docker build -f CodeSmith.Api/Dockerfile .`
