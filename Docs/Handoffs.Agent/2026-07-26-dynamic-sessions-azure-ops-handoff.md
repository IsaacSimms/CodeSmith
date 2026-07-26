# Thread Handoff Document

> **Handoff Mode: Implementation**  
> **Receiving agent job: Resume and continue — finish Azure session-pool create + role + API env + smoke; code is already landed unless smoke fails**

---

### 1. Thread Purpose (2–4 sentences)

Finish **Azure-side** wiring for Tutoring **Test Code** using ACA **custom Dynamic Sessions** + the multi-lang `CodeSmith.Executor` image. App code, adapter, DI, executor project, deploy-executor workflow, and ops runbook were already implemented in an earlier thread (2026-07-22). This thread confirmed readiness, walked square-one ops against the live RG, and got as far as **failed** `az containerapp sessionpool create` attempts that taught the correct flags. User stopped for the night; next thread continues ops with **`ready-sessions 1`** (platform minimum).

---

### 2. Stack & Environment

- **Backend:** .NET 8 CodeSmith; `ICodeExecutionService` Seam; `DynamicSessionsCodeExecutionService` Adapter when `CodeExecution:Backend=DynamicSessions`
- **Local sandbox:** Piston (default) — leave alone
- **Azure prod (confirmed in portal + CLI):**
  - Sub: `sub-primiProdCodeSmith-cus-001` (`b4449a41-633d-420e-8939-23c0f7a72e40`)
  - RG: `rg-codesmith-prod-centralus-001` (Central US)
  - CAE: `cae-codesmith-prod-centralus-001`
  - API CA: `ca-codesmith-api-001`
  - ACR: `acrcodesmithprod001`
  - MI: `mi-codesmith-backend-prod-centralus-001`
  - Also present: Key Vault, SQL, SWA, App Insights — not needed for this path
- **CLI:** Windows PowerShell; user `az login` on correct sub; `containerapp` extension (preview) installed during first create attempt
- **Not creating:** a second always-on Container App for Piston (rejected design; ACA forbids privileged)

---

### 3A. What Was Accomplished

1. **Confirmed codebase ready** for Azure ops: request DTO, three adapters, executor image project, DI switch, `POST /api/session/{id}/run`, TerminalPanel cold-start UX, unit tests, `Docs/general/dynamic-sessions-azure-setup.md`, `.github/workflows/deploy-executor.yml`.
2. **Corrected terminology for user:** not “another Container App”; **session pool** + BYO executor image + Hyper-V isolation.
3. **Phase 0 foundation verified** via RG screenshot + CLI — all base resources exist.
4. **Executor image already in ACR** (no push needed this session):
   - Tags: `latest`, `7719cac96f7af8280c68445f46da1f053ba2ae66`
   - Note: `az acr repository list` only showed `codesmith-api`; `show-tags` on `codesmith-executor` still works — list quirk.
5. **AcrPull granted** (or re-confirmed) for MI on ACR via `az role assignment create --role AcrPull` — succeeded (JSON role assignment returned).
6. **Learned create failures and fixes** (see §6) — location, registry identity, no dual MI flags, ready sessions ≥ 1.
7. **User decision for handoff:** proceed with **`ready-sessions 1`** (minimum Azure allows), accept always-on idle cost for custom pools; create was **not** completed before sleep.

---

### 4A. Current State

| Item | State |
|------|--------|
| App / executor / GH workflow code | **Complete** (prior thread) |
| Executor image in ACR | **Present** (`latest` + sha) |
| AcrPull on MI → ACR | **Done** (this session) |
| Session pool `sp-codesmith-exec-001` | **Not created** (all create attempts failed) |
| Session Executor role on pool | **Not done** (blocked on pool) |
| API env `CodeExecution__Backend=DynamicSessions` + pool endpoint | **Not set** |
| Live smoke | **Not done** |
| Local Piston | Unchanged default |

**You are here:** next action is re-run `az containerapp sessionpool create` with the **validated flag set** in §7.

---

### 5. Key Decisions & Rationale

| Decision | Rationale |
|----------|-----------|
| Custom Dynamic Sessions, not Piston CA | ACA forbids privileged; Piston needs them locally |
| Reuse tutoring `sessionId` as pool `identifier` | Warm multi-run within a problem |
| `ready-sessions 1` (user accepted for continuation) | Azure rejects `0`: must be **> 0** and **< maxConcurrentSessions** |
| `max-sessions 3` | Cap concurrent sandboxes; ready must stay &lt; max |
| Image pull via `--registry-identity $MI_ID` only | Private ACR; do **not** also pass same MI as `--mi-user-assigned` (duplicate identity JSON error) |
| EgressDisabled | Untrusted student code |
| No usage metering for code runs | Infra cost, not LLM |
| Near-free idle like scale-to-zero CA is **impossible** while pool exists | Custom pools require ready ≥ 1 and bill dedicated-ish capacity; only true $0 is delete the pool |

---

### 6. Blockers & Open Questions

| Item | Tried | Next |
|------|--------|------|
| Create failed: `Location 'None'` | Added `--location centralus` | Keep location |
| Create failed: missing `registryCredentials.username or identity` | Added `--registry-identity $MI_ID` | Keep it; AcrPull already set |
| Create failed: **duplicate** `userAssignedIdentities` for same MI | Had both `--registry-identity` and `--mi-user-assigned` with same `$MI_ID` | **Omit `--mi-user-assigned`** |
| Create failed: `readySessionInstances` invalid at `0` | Platform requires &gt; 0 and &lt; max | Use `ready-sessions 1`, `max-sessions 3` |
| Idle cost concern | Documented honestly | User chose ready=1 for now; monitor Cost Management; optional delete pool when unused |
| Exact CLI flags vary by extension version | Preview containerapp extension auto-installed | If create still fails, `az containerapp sessionpool create -h` |

---

### 7. Next Steps (Ordered)

1. **Confirm shell context** (PowerShell, correct sub):
   ```powershell
   az account show --query "{name:name, id:id}" -o table
   ```
2. **Refresh `$MI_ID`** if new shell:
   ```powershell
   $MI_ID = az identity show `
     -n mi-codesmith-backend-prod-centralus-001 `
     -g rg-codesmith-prod-centralus-001 `
     --query id -o tsv
   ```
3. **Create session pool** (do **not** add `--mi-user-assigned`):
   ```powershell
   az containerapp sessionpool create `
     --name sp-codesmith-exec-001 `
     --resource-group rg-codesmith-prod-centralus-001 `
     --environment cae-codesmith-prod-centralus-001 `
     --location centralus `
     --container-type CustomContainer `
     --image acrcodesmithprod001.azurecr.io/codesmith-executor:latest `
     --cpu 1.0 `
     --memory 2Gi `
     --target-port 8080 `
     --ready-sessions 1 `
     --max-sessions 3 `
     --cooldown-period 300 `
     --network-status EgressDisabled `
     --registry-server acrcodesmithprod001.azurecr.io `
     --registry-identity $MI_ID
   ```
4. **Verify pool:**
   ```powershell
   az containerapp sessionpool show `
     --name sp-codesmith-exec-001 `
     --resource-group rg-codesmith-prod-centralus-001 `
     --query "{name:name, endpoint:properties.poolManagementEndpoint, ready:properties.scaleConfiguration.readySessionInstances, max:properties.scaleConfiguration.maxConcurrentSessions, nodeCount:properties.nodeCount}" -o json
   ```
5. **Grant Session Executor** to API MI on the pool:
   ```powershell
   $POOL_ID = az containerapp sessionpool show `
     --name sp-codesmith-exec-001 `
     --resource-group rg-codesmith-prod-centralus-001 `
     --query id -o tsv
   $PRINCIPAL_ID = az identity show `
     -n mi-codesmith-backend-prod-centralus-001 `
     -g rg-codesmith-prod-centralus-001 `
     --query principalId -o tsv
   az role assignment create `
     --role "Azure ContainerApps Session Executor" `
     --assignee $PRINCIPAL_ID `
     --scope $POOL_ID
   ```
6. **Point API CA at Dynamic Sessions:**
   ```powershell
   $POOL_EP = az containerapp sessionpool show `
     --name sp-codesmith-exec-001 `
     --resource-group rg-codesmith-prod-centralus-001 `
     --query properties.poolManagementEndpoint -o tsv
   az containerapp update `
     -n ca-codesmith-api-001 `
     -g rg-codesmith-prod-centralus-001 `
     --set-env-vars `
       "CodeExecution__Backend=DynamicSessions" `
       "CodeExecution__DynamicSessions__PoolManagementEndpoint=$POOL_EP" `
       "CodeExecution__DynamicSessions__ExecutePath=/execute" `
       "CodeExecution__DynamicSessions__TimeoutSeconds=120"
   ```
   Ensure API uses the same MI for outbound tokens (`DefaultAzureCredential` / `AZURE_CLIENT_ID` if multi-MI). API image must already contain DynamicSessions adapter code.
7. **Optional:** raise API ingress request timeout toward **120s** if cold/warm allocation still times out.
8. **Smoke:** prod Tutoring → Test Code (may show “Starting sandbox…”) → second run same session → spot-check languages.
9. **Only if smoke fails:** fix token/role/endpoint/contracts — do not re-litigate host choice unless user reopens cost/host strategy.
10. **Ops hygiene (user-aware):** custom pool with ready=1 is always-on cost; watch Cost Analysis / `nodeCount`. Deleting the pool is the only true scale-to-zero.

---

### 8. Must-Knows for the New Thread

- **Do not** deploy privileged Piston as a Container App.
- **Do not** reintroduce a separate `IPistonClient` seam; use `ICodeExecutionService`.
- **Do not** meter code execution with `IUsageEnforcer` unless user reopens that.
- Dynamic Sessions call shape: `POST {PoolManagementEndpoint}{ExecutePath}?identifier={sessionId}` with Bearer token audience `https://dynamicsessions.io/.default` (code already does this).
- Identifier: 4–128 chars; GUID `"D"` format is valid; Tutoring passes `sessionId`.
- **CLI traps already burned:**
  1. Need `--location centralus`
  2. Need `--registry-identity` for private ACR (not only `--registry-server`)
  3. **Never** combine `--registry-identity` + `--mi-user-assigned` with the **same** MI (duplicate identity path)
  4. `--ready-sessions 0` is **invalid** — use **1**
- User tone: no affirmations; direct; UL (Module/Seam/Adapter) when discussing architecture; explicit approval before code changes — this thread is **ops**, not app code unless smoke exposes a bug.
- Prior ideation/impl recaps: `Docs/Recaps/2026-07-22-azure-dynamic-sessions-sandbox.md`, `Docs/Handoffs.User/2026-07-22-dynamic-sessions-azure-handoff.md` — still valid for product design; **ready=0 is obsolete** vs live Azure.
- Token-streaming handoff is orthogonal (LLM NDJSON); only useful for shared Azure resource names.

---

### 9. Relevant Artifacts

| Path | Role | State |
|------|------|--------|
| `CodeSmith.Infrastructure/Services/DynamicSessions/*` | Adapter + token provider | Complete |
| `CodeSmith.Executor/` + `Dockerfile` | Session container image | Complete; image in ACR |
| `CodeSmith.Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` | Backend switch | Complete |
| `.github/workflows/deploy-executor.yml` | Push executor to ACR | Exists; not required this session (image already present) |
| `Docs/general/dynamic-sessions-azure-setup.md` | Ops runbook | Complete but **stale on ready=0** and dual-MI flags — prefer this handoff’s create command |
| `Docs/Recaps/2026-07-22-azure-dynamic-sessions-sandbox.md` | Prior impl recap | Complete |
| `Docs/Handoffs.User/2026-07-22-dynamic-sessions-azure-handoff.md` | Prior handoff | Complete; ops remaining then; still remaining now |

---

**Paste into new thread:**

> Picking up from a previous session. Here's the handoff: [paste this document]  
> Confirm you have context and flag anything unclear before we continue. Primary work is Azure session-pool create with ready-sessions 1, then role + API env + smoke per §7; app code already landed unless smoke fails.
