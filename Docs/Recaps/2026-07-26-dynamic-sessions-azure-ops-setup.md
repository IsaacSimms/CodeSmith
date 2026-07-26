# Dynamic Sessions Azure Ops Setup (Session Pool Create)

**Date:** 2026-07-26  
**Type:** ops  
**Environment / Systems:** CodeSmith prod Azure (`rg-codesmith-prod-centralus-001`, Central US), Windows PowerShell + Azure CLI

## TL;DR

App code for custom Dynamic Sessions was already complete; this thread started live Azure ops. Executor image was already in ACR. Session pool create failed through several CLI/platform constraints; the correct create shape was established but the pool was **not** created before the session ended. User will continue with **`ready-sessions 1`** (platform minimum).

## Context & Goal

Confirm the codebase was ready for Azure run-code infra, then provision the missing Azure pieces for Tutoring **Test Code**: session pool, Session Executor role, API env pointing at `DynamicSessions`. Prior work (2026-07-22) had shipped Core/Infrastructure/Executor/Web/CI and a runbook; production pool wiring remained.

## Key Points Explored

- **Architecture:** Not a second always-on Container App. Production path is ACA **custom Dynamic Sessions** (session pool + Hyper-V sandboxes + `codesmith-executor` image). Local stays Piston.
- **Foundation already present:** CAE, API CA, ACR, MI, SQL, SWA, Key Vault in `rg-codesmith-prod-centralus-001`.
- **Executor image already in ACR:** tags `latest` and `7719cac96f7af8280c68445f46da1f053ba2ae66` (`az acr repository list` only showed `codesmith-api`; `show-tags` for `codesmith-executor` still worked).
- **AcrPull:** MI `mi-codesmith-backend-prod-centralus-001` granted/confirmed **AcrPull** on `acrcodesmithprod001`.
- **Create failures and root causes:**
  1. `Location 'None' is not currently supported` → need `--location centralus`
  2. Missing `registryCredentials.username or identity` → need `--registry-identity $MI_ID` for private ACR
  3. Duplicate `userAssignedIdentities` → do **not** pass the same MI as both `--registry-identity` and `--mi-user-assigned`
  4. `readySessionInstances` invalid at `0` → Azure requires ready **> 0** and **&lt; maxConcurrentSessions**
- **Cost:** Custom session pools cannot idle near free like a scale-to-zero Container App while the pool exists. Ready ≥ 1 implies always-on capacity (Dedicated-plan-style billing for custom pools). True $0 idle = delete the pool. User accepted ready=1 for continuation.

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Host remains custom Dynamic Sessions | Confirmed; no Piston CA |
| Proceed with `ready-sessions 1`, `max-sessions 3` | User choice for next session despite idle cost |
| Registry auth via MI only (`--registry-identity`) | Correct for private ACR; omit dual MI assign |
| Pool create this session | **Failed / not created** — ops incomplete |
| Code changes | None — ops-only thread |

**Validated create command (not yet successfully run):**

```powershell
az containerapp sessionpool create `
  --name sp-codesmith-exec-001 `
  --resource-group rg-codesmith-prod-centralus-001 `
  --environment cae-codesmith-prod-centralus-001 `
  --location centralus `
  --container-type CustomContainer `
  --image acrcodesmithprod001.azurecr.io/codesmith-executor:latest `
  --cpu 1.0 --memory 2Gi --target-port 8080 `
  --ready-sessions 1 --max-sessions 3 --cooldown-period 300 `
  --network-status EgressDisabled `
  --registry-server acrcodesmithprod001.azurecr.io `
  --registry-identity $MI_ID
```

## Open Questions / Next Steps

1. Run the create command above and verify pool + `poolManagementEndpoint`.
2. Grant **Azure ContainerApps Session Executor** to the API MI on the pool.
3. Set API env: `CodeExecution__Backend=DynamicSessions`, pool endpoint, `/execute`, 120s timeout.
4. Optional: API ingress timeout ≥ 120s.
5. Prod smoke: Test Code cold/warm; spot-check languages.
6. Monitor Cost Management / `nodeCount` for ready=1 idle spend.
7. Update `Docs/general/dynamic-sessions-azure-setup.md` when convenient — still documents `ready=0` and dual-MI patterns that Azure rejects.

## Artifacts

| Path | State |
|------|--------|
| `Docs/Handoffs.Agent/2026-07-26-dynamic-sessions-azure-ops-handoff.md` | Forward handoff for next session — complete |
| `Docs/general/dynamic-sessions-azure-setup.md` | Ops runbook — partially stale on ready/MI flags |
| `Docs/Recaps/2026-07-22-azure-dynamic-sessions-sandbox.md` | Prior implementation recap (code shipped) |
| `.github/workflows/deploy-executor.yml` | Exists; image already in ACR this session |
| Session pool `sp-codesmith-exec-001` | **Not created** |
