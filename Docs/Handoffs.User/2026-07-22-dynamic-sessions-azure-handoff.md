# Thread Handoff Document

> **Handoff Mode: Implementation**  
> **Receiving agent job: Resume and continue — finish Azure session-pool provisioning and live smoke; only touch code if ops or smoke exposes gaps**

---

### 1. Thread Purpose (2–4 sentences)

Ship sandboxed multi-language **Test Code** for CodeSmith in Azure at personal-project cost (scale-to-zero, no always-on executor). An earlier ideation handoff proposed privileged Piston as a Container App plus a new client seam; this thread pressure-tested that design, rejected it, and **implemented** the replacement: ACA **custom Dynamic Sessions** + multi-lang executor image + thin Adapter behind the existing `ICodeExecutionService`. Code and CI are in-repo; **live Azure pool create/config and prod smoke remain**.

---

### 2. Stack & Environment

- Backend: .NET 8, Clean Architecture (Core / Infrastructure / Api)
- Frontend: React 19, Vitest
- Local sandbox: Piston via `docker-compose.yml` (`ghcr.io/engineer-man/piston`, privileged)
- Azure: RG `rg-codesmith-prod-centralus-001`, CAE `cae-codesmith-prod-centralus-001`, API app `ca-codesmith-api-001`, MI `mi-codesmith-backend-prod-centralus-001`, ACR `acrcodesmithprod001`
- New: custom Dynamic Sessions session pool (not yet created in Azure from this thread), executor image `codesmith-executor`
- Region: Central US (Dynamic Sessions supported)

---

### 3A. What Was Accomplished

1. **Grilled the ideation handoff** — confirmed Piston ACA path is invalid (no privileged containers); mapped Azure-viable options; locked custom Dynamic Sessions.
2. **Widened the execution Seam** — `CodeExecutionRequest` (`Language`, `Code`, `Guid? SessionId`); `ICodeExecutionService.ExecuteAsync(CodeExecutionRequest, ct)`; `TutoringService.RunCodeAsync` passes tutoring `sessionId`.
3. **Updated existing Adapters** — `PistonCodeExecutionService` and `LocalProcessCodeExecutionService` ignore `SessionId`; tests updated.
4. **Added DynamicSessions Adapter** — `DynamicSessionsCodeExecutionService`, `IDynamicSessionsTokenProvider` / `DefaultAzureDynamicSessionsTokenProvider` (audience `https://dynamicsessions.io/.default`), contracts aligned with executor, DI branch `Backend=DynamicSessions`, package `Azure.Identity`.
5. **Built `CodeSmith.Executor`** — Minimal API `POST /execute`, `GET /health`, `GET /ready`; subprocess runners for python, typescript, go, cpp, rust, java, csharp (dotnet SDK); multi-lang `Dockerfile`; added to `CodeSmith.slnx`.
6. **Frontend** — `TerminalPanel` shows “Starting sandbox…” after 5s pending (cold-start UX).
7. **Ops artifacts** — `.github/workflows/deploy-executor.yml`; `Docs/general/dynamic-sessions-azure-setup.md`.
8. **Config** — `CodeExecution:DynamicSessions` section in `appsettings.json` (empty pool endpoint locally).
9. **API Dockerfile** — copies `CodeSmith.Executor.csproj` so `dotnet restore CodeSmith.slnx` still works.
10. **Verified** — full Release build clean; 423 non-integration backend tests passed; TerminalPanel tests passed.

---

### 4A. Current State

| Layer | State |
|-------|--------|
| Repo code (request DTO, adapters, executor, DI, UI, workflow, runbook) | **Complete** |
| Executor image in ACR | **Not pushed** (run Deploy Executor workflow) |
| Azure session pool | **Not created** |
| MI Session Executor role on pool | **Not granted** |
| API Container App env (`Backend=DynamicSessions`, pool endpoint, 120s timeout) | **Not set** |
| Live multi-language smoke in prod | **Not done** |
| Local Piston path | **Unchanged / still default** |

Nothing in the code path is known-broken; production simply still points at whatever it had before (likely Piston BaseUrl that does not resolve in Azure).

---

### 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Reject plain Piston Container App | ACA forbids privileged containers; Piston’s isolation model needs them locally. |
| Custom Dynamic Sessions | Hyper-V isolation around BYO image; multi-lang; Azure-native; no nested Docker. |
| All 7 languages in one image | Product parity with Tutoring `Language` enum; idle $ unaffected at `readySessionInstances=0`; cost is cold-start latency. |
| `readySessionInstances=0` | Personal-project cost; accept long first Test Code after idle. |
| Reuse tutoring `sessionId` as pool `identifier` | Warm multi-run within a problem; requires `SessionId` on the Seam. |
| `CodeExecutionRequest` DTO (not extra optional params forever) | Room for stdin/args later; one call-site reshape. |
| Keep local Piston | Zero extra day-to-day cost; offline fallback; Azure-first testing still fine. |
| Egress disabled | Untrusted student code must not phone home. |
| C# via dotnet SDK (not mono) | Better modern C# for LLM-generated code; local Piston may still use mono. |
| No usage/credit debit for runs | Infrastructure cost, not LLM spend; defer. |
| Image via GH workflow; pool via one-time CLI | Matches existing `deploy-azure.yml` discipline. |

---

### 6. Blockers & Open Questions

| Item | Notes | Next step |
|------|--------|-----------|
| Session pool not provisioned | Blocked only on human Azure access | Follow `Docs/general/dynamic-sessions-azure-setup.md` |
| Exact `az containerapp sessionpool` CLI flags | Vary by CLI version; runbook is example | Confirm with `az containerapp sessionpool create -h` or portal |
| API ingress request timeout | Cold start may exceed 60s | Raise if Test Code dies before sandbox returns |
| Executor image size / cold start | Expected ~30–90s first run after idle | Measure after pool exists; do **not** set ready>0 unless UX forces it |
| C# first-run NuGet restore inside session | `dotnet run` may be slow cold | Image warms templates; measure; optional pre-restore later |

---

### 7. Next Steps (Ordered)

1. **Run Deploy Executor** workflow (`workflow_dispatch`) — push `acrcodesmithprod001.azurecr.io/codesmith-executor:<sha>` and `:latest`.
2. **Create custom container session pool** (`sp-codesmith-exec-001` or similar) in existing CAE: image from ACR, target port **8080**, `readySessionInstances=0`, max concurrent ~3, cooldown ~300s, **EgressDisabled**, probes `/health` + `/ready`.
3. **Grant** MI `mi-codesmith-backend-prod-centralus-001` role **Azure ContainerApps Session Executor** on the pool scope.
4. **Configure** `ca-codesmith-api-001`:
   - `CodeExecution__Backend=DynamicSessions`
   - `CodeExecution__DynamicSessions__PoolManagementEndpoint=<from az show>`
   - `CodeExecution__DynamicSessions__ExecutePath=/execute`
   - `CodeExecution__DynamicSessions__TimeoutSeconds=120`
   - Ensure MI / `AZURE_CLIENT_ID` so `DefaultAzureCredential` works for `https://dynamicsessions.io/.default`
5. **Ingress timeout** on API app ≥ 120s if platform default is lower.
6. **Smoke:** Tutoring → Test Code (expect sandbox message on first cold run) → second Test Code warmer → spot-check all 7 languages.
7. Only if smoke fails: fix Adapter/executor contracts or token/role misconfig — do not re-litigate host choice.

---

### 8. Must-Knows for the New Thread

- Do **not** reintroduce `IPistonClient` or deploy privileged Piston on ACA.
- Do **not** meter code execution with `IUsageEnforcer` unless the user reopens that decision.
- Dynamic Sessions call shape: `POST {PoolManagementEndpoint}{ExecutePath}?identifier={sessionId}` with `Authorization: Bearer` token for audience `https://dynamicsessions.io`.
- Identifier constraints: 4–128 chars; GUID `"D"` format is valid.
- Local: `CodeExecution:Backend=Piston`, `BaseUrl=http://localhost:2000`.
- Conventions: `// == Title == //` block comments; no member `/// <summary>`; TDD preferred; edit-in-place.
- UL: `ICodeExecutionService` is the Seam; DynamicSessions is an Adapter; executor image is remote implementation detail.
- User is Azure-first for validation; local Piston rarely used but must stay green.
- Verification bar for code: `dotnet build` + `dotnet test` unless user asks for live Azure.

---

### 9. Relevant Artifacts

| Path | Role | State |
|------|------|--------|
| `CodeSmith.Core/Models/CodeExecutionRequest.cs` | Request DTO | Complete |
| `CodeSmith.Core/Interfaces/ICodeExecutionService.cs` | Seam | Complete |
| `CodeSmith.Infrastructure/Services/DynamicSessions/*` | Adapter + token + contracts | Complete |
| `CodeSmith.Infrastructure/Configuration/CodeExecutionOptions.cs` | Includes `DynamicSessionsOptions` | Complete |
| `CodeSmith.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` | Backend switch | Complete |
| `CodeSmith.Executor/Program.cs` + `Dockerfile` | Session container | Complete |
| `CodeSmith.Web/.../TerminalPanel.tsx` | Cold-start UX | Complete |
| `.github/workflows/deploy-executor.yml` | Push executor image | Complete |
| `Docs/general/dynamic-sessions-azure-setup.md` | One-time Azure runbook | Complete |
| `Docs/Recaps/2026-07-22-azure-dynamic-sessions-sandbox.md` | Backward-looking recap | Complete |
| Prior ideation handoff (Piston CA) | Superseded | Do not implement as written |

---

**Paste into new thread:**

> Picking up from a previous session. Here's the handoff: [paste this document]  
> Confirm you have context and flag anything unclear before we continue. Primary work is Azure pool provisioning + prod smoke per §7; code is already landed unless smoke fails.
