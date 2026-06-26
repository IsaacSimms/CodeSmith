# Azure Deploy Seam (Dockerfile + GitHub Actions to ACR + Container Apps)

**Date:** 2026-06-19
**Type:** implementation
**Environment / Systems:** .NET 8, Windows + PowerShell, Azure Container Apps (ca-codesmith-api-001), ACR (acrcodesmithprod001), GitHub Actions

## TL;DR

Added the missing production deploy seam for CodeSmith.Api: root `.dockerignore`, `CodeSmith.Api/Dockerfile` (multi-stage .NET 8), and `.github/workflows/deploy-azure.yml` (`workflow_dispatch` only). This allows safely promoting the June 18 usage enforcement work (and all future changes) from GitHub to the existing `ca-codesmith-api-001` Container App via `acrcodesmithprod001` ACR. Also added `mcps/` to `.gitignore` to stop tool schema JSON noise from appearing in source control.

## Context & Goal

The repo had complete code (including the June 18 SaaS cost protection seams) but zero path to the pre-provisioned Azure resources. No Dockerfile, no CI/CD. The user provided a detailed thread-handoff spec and required plan mode + grilling before implementation. Goal: minimal, repeatable, manual-gated deploy seam following all project conventions. Agent was forbidden from running any `az` commands.

## Key Points Explored

- Multi-stage Dockerfile using `mcr.microsoft.com/dotnet/sdk:8.0` then `aspnet:8.0`, restoring via root `CodeSmith.slnx`, publishing only CodeSmith.Api.
- Need to handle slnx references to excluded projects (Web .esproj, CLI, Tests) → used .dockerignore negation patterns to re-include only the tiny project manifest files.
- EF Design-time gotcha: `Microsoft.EntityFrameworkCore.Design` has `PrivateAssets="all"`; DbContext registration in ServiceCollectionExtensions.cs is conditional on `ConnectionStrings:CodeSmithDb`.
- Workflow constraints: `workflow_dispatch` only, Service Principal via four GitHub secrets, `az acr login` + `docker buildx build --push`, then `az containerapp update`.
- GitHub runner has no az CLI by default → included install step.
- Port: EXPOSE 8080 (rely on aspnet base image + existing Container App ingress config).
- Later discovery: MCP servers (grok_com_github etc.) write dozens of tool schema JSONs into `mcps/*/tools/*.json`; these polluted Source Control view.

## Decisions & Outcomes

- Created exactly the three files specified in the handoff (no edits to existing source).
- Chose negation + full slnx restore (per user choice during grill) for .dockerignore + Dockerfile.
- Tag with `${{ github.sha }}` only (no :latest for v1).
- Workflow uses literal `az` commands + `docker/setup-buildx-action` after installing az CLI.
- Documented the EF gotcha in the Dockerfile with clear block comment.
- Added `mcps/` to `.gitignore` to hide assistant-generated tool schemas.
- Followed all conventions: block comments (adapted with `#` for Dockerfile/YAML), no member `/// <summary>`, Ubiquitous Language (Seam, deploy seam), plan mode + exit, explicit user approval before code.
- Verification commands provided for local `docker build`.

## Open Questions / Next Steps

- User must perform one-time Azure setup (create ACR if not done, grant AcrPull to the Managed Identity, create GitHub Service Principal + store 4 secrets).
- One-time Container App ingress target-port alignment to 8080 if it was set to 80.
- Run the workflow manually after merge + tests.
- Future: consider adding `api` service to docker-compose.prod.yml overlay (per README guidance); OIDC instead of SP secrets.
- Migrations remain a separate job.

## Artifacts

- `.dockerignore` (root)
- `CodeSmith.Api/Dockerfile`
- `.github/workflows/deploy-azure.yml`
- `.gitignore` (added `mcps/` rule)
- Session plan: `.../019ee303-.../plan.md` (approved)
- Original detailed spec: the long thread-handoff pasted at start of conversation (matches the "What Must Be Produced" section)
- `Handoffs/2026-06-19-azure-deploy-seam-handoff.md` (forward-looking)
- `Recaps/2026-06-19-azure-deploy-seam.md` (this file)