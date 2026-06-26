# Thread Handoff Document

> **Handoff Mode: Implementation**  
> **Receiving agent job: Help with the project going forward. Use the new Azure deploy seam to promote changes. Follow all existing conventions and architecture.**

---

### 1. Thread Purpose (2–4 sentences)

This thread added the production deploy seam for CodeSmith.Api so that code (including the June 18 usage enforcement seams and all future work) can be promoted from the GitHub repo to the existing `ca-codesmith-api-001` Azure Container App via ACR. The repo previously had complete application code but no Dockerfile, no `.dockerignore`, and no CI/CD. A detailed implementation handoff spec was provided at the start; work followed plan mode + grilling, then produced exactly three new files. The seam is manually triggered (`workflow_dispatch`) to keep a human gate between merge + tests and production.

---

### 2. Stack & Environment

- Backend: .NET 8, ASP.NET Core Web API (`CodeSmith.Api`)
- Solution: `CodeSmith.slnx` (authoritative, at repo root)
- Projects in the container image: only `CodeSmith.Core`, `CodeSmith.Infrastructure`, `CodeSmith.Api`
- Excluded from image: `CodeSmith.Web` (esproj/React), `CodeSmith.CLI`, `CodeSmith.Tests`
- Deployment target: Azure Container Apps (`ca-codesmith-api-001` in `rg-codesmith-prod-centralus-001`)
- Registry: Azure Container Registry (`acrcodesmithprod001`)
- Auth: Service Principal (4 GitHub secrets) for CI; Managed Identity on the App for runtime ACR pull + Azure SQL
- AI: Anthropic (primary), OpenAI, xAI via the `ILlmServiceFactory` + keyed services + usage decorators
- Data: EF Core + Azure SQL (connection string "CodeSmithDb" — DbContext registration is conditional)
- Platform: Windows (dev), PowerShell, Docker Desktop (for local verification)
- CI/CD: GitHub Actions (no auto-deploy on push to main)

---

### 3A. What Was Accomplished

- Created root `.dockerignore` with standard .NET + Node exclusions + negation patterns (`!CodeSmith.Web/CodeSmith.Web.esproj` etc.) so `dotnet restore CodeSmith.slnx` succeeds while keeping the build context small.
- Created `CodeSmith.Api/Dockerfile`:
  - Multi-stage: `sdk:8.0` (restore via slnx + csprojs, publish) → `aspnet:8.0` (final).
  - `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "CodeSmith.Api.dll"]`.
  - Clear `# == EF Design-Time / DbContext Gotcha == #` block comment explaining the Design package PrivateAssets and conditional `UseSqlServer`.
- Created `.github/workflows/deploy-azure.yml`:
  - `workflow_dispatch` trigger only.
  - `azure/login@v2` using four secrets.
  - Installs az CLI, logs into ACR, runs `docker buildx build --push` with tag `${{ github.sha }}`, then `az containerapp update`.
  - Top comment block explains the seam and relationship to June 18 work.
- Followed plan mode strictly (exploration → design → write plan.md → exit_plan_mode + user approval) before any file creation.
- Used `/grill-me` (via repeated `ask_user_question`) to resolve ambiguities (slnx vs project restore, az cli install vs azure/CLI action, EXPOSE port, tag strategy, dockerignore scope).
- Added `mcps/` to `.gitignore` to prevent MCP tool schema JSON files (from connected GitHub/Playwright/VS-Debugger servers) from polluting Source Control.
- All work respected project rules: block comments (`// == Title == //` or `# ==` adapted), no member-level `/// <summary>`, edit-in-place preference, Ubiquitous Language, TDD spirit where applicable.

---

### 4A. Current State

- The three deploy artifacts exist and match the original handoff spec.
- `.dockerignore`, `CodeSmith.Api/Dockerfile`, and the workflow are ready.
- `mcps/` is now ignored.
- The June 18 usage enforcement seams (`UsageEnforcer`, decorators, `IUsageEnforcer`, EF entities, `HttpCurrentUser`, 402 handling, `[Authorize]` on spending endpoints) are in the codebase and can now be promoted.
- Container App is still running the stock aspnet image until the user runs the workflow.
- Current branch at time of handoff: `refactor/llm-completion-seam` (significant in-flight refactoring of LLM service interfaces and decorators toward a single `UsageEnforcingLlmService`).
- No changes were made to `Program.cs`, controllers, csprojs, README, docker-compose.yml, or any business logic.

You are here: the missing "image build + revision update" seam now exists in front of the usage enforcement seam.

---

### 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| `workflow_dispatch` only (manual) | Prevents accidental deploys; developer merges, tests, then explicitly promotes. |
| Restore via full `CodeSmith.slnx` + negation in .dockerignore | Keeps slnx as the single source of truth for dev while allowing clean Docker context. |
| Tag only with `${{ github.sha }}` (no :latest in v1) | Immutable, traceable deploys; Container App revision history shows exact commit. |
| EXPOSE 8080 with no ASPNETCORE_* overrides | Matches aspnet:8.0 base image default; Container App ingress target port is configured on the App, not the image. |
| az CLI install step in workflow | ubuntu-latest does not ship az; needed for literal `az acr login` + `az containerapp update` in host context. |
| Document EF gotcha inside Dockerfile | Future migration or design-time work inside/against the image will fail without the connection string. |
| `mcps/` in .gitignore | These are generated assistant tool schemas, not project source. |

---

### 6. Blockers & Open Questions

- User must run the one-time Azure setup commands (ACR creation/grants if not complete, Service Principal + 4 secrets in GitHub).
- Container App ingress target port may need a one-time manual update to 8080.
- Migrations are deliberately not part of this workflow (conditional DbContext + separate job).
- Full end-to-end verification (build + workflow dispatch + live smoke) can only be done by the user.

---

### 7. Next Steps (Ordered)

1. **Local verification (user machine with Docker Desktop):**  
   `docker build -f CodeSmith.Api/Dockerfile -t codesmith-api:verify .`

2. Perform Azure one-time setup (if not already done) and store the four secrets.

3. (Optional but recommended) Align Container App ingress target port once:  
   `az containerapp ingress update --name ca-codesmith-api-001 --resource-group rg-codesmith-prod-centralus-001 --target-port 8080`

4. Merge the deploy seam changes to `main`, run full tests, then manually trigger the "Deploy Azure" workflow.

5. After successful deploy, exercise a protected endpoint (with real identity) to confirm the June 18 usage seams are live in prod.

6. Future work items (not started in this thread): add api service to docker-compose overlays, replace SP with OIDC, add migration job, etc.

---

### 8. Must-Knows for the New Thread (Project Conventions & State)

- **Ubiquitous Language (use exactly):** Module, Interface, Implementation, Seam, Adapter, Depth, Leverage, Locality. Say "Seam" not "boundary" or "service".
- **Block comment style:** Always start important blocks with `// == Title Here == //` (or `# ==` for Dockerfile/YAML). Never use member-level `/// <summary>`.
- **Change discipline:** Prefer editing existing files. Create new only when absolutely required. The three deploy files were an explicit exception because none existed.
- **Plan mode:** For any ambiguous or high-impact change, enter plan mode first (explore → design → write plan.md in the session folder → exit_plan_mode). User must explicitly approve.
- **Grilling:** When the user says "/grill-me" or asks to pressure-test, use `ask_user_question` one question at a time with options + your recommendation.
- **Deploy seam:** Use the GitHub Action for promotion. Never run `az` commands yourself unless the user explicitly asks you to guide them.
- **EF note:** DbContext only gets a provider when `ConnectionStrings:CodeSmithDb` is present. Design package is PrivateAssets=all.
- **MCP noise:** `mcps/` is ignored. Do not commit those JSONs.
- **Testing:** Backend uses xUnit + NSubstitute. Frontend: Vitest + RTL. E2E: Playwright (only when explicitly asked).
- **Current known in-flight work:** Significant refactoring on the LLM call path (collapsing the three capability interfaces + three decorators into one `UsageEnforcingLlmService` + clean `ILlmService`).

---

### 9. Relevant Artifacts

- **New deploy seam (complete):**
  - `.dockerignore` (root)
  - `CodeSmith.Api/Dockerfile`
  - `.github/workflows/deploy-azure.yml`
- **Project root docs (read these first):**
  - `Claude.md` (global) + `CodeSmith/Claude.md` (project-specific)
  - `README.md` (contains the original "write a Dockerfile" guidance)
  - `Handoffs/2026-06-18-usage-enforcement-handoff.md` (the seams now promotable)
  - `Recaps/2026-06-18-usage-enforcement-buildout.md`
- **Key code locations:**
  - `CodeSmith.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` (conditional DbContext ~lines 40-45, all service wiring)
  - `CodeSmith.slnx` (always restore/publish target for containers)
  - `CodeSmith.Api/Program.cs` (middleware pipeline, minimal auth skeleton)
- **Session artifacts from this thread:**
  - The long original handoff spec pasted by user at thread start
  - Approved plan at the session folder (used for implementation)
- **Git state at handoff:** On `refactor/llm-completion-seam`. Many files changed from LLM seam refactor (deleted old interfaces, new single enforcer, etc.). Our three files + .gitignore update are the clean additions from this thread.

---

**Paste into new thread:**

"Picking up from a previous session. Here's the handoff: [paste this entire document]

Confirm you have context and flag anything unclear before we continue. Especially confirm you understand the deploy seam, the Ubiquitous Language terms, the block comment convention, plan mode requirement, and current state of the LLM seam refactor."