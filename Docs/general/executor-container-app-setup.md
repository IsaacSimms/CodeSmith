# Executor Container App — Azure Setup

One-time ops to host `CodeSmith.Executor` as a **scale-to-zero Azure Container App** backing Tutoring **Test Code**.

Replaces the custom Dynamic Sessions session-pool path, which could not scale to zero: Azure rejects `--ready-sessions 0` on custom pools, so the cheapest pool still bills one always-warm session. The `DynamicSessions` Adapter remains in the codebase as the upgrade path if microVM isolation is ever required.

## Why this is safe enough

ACA is a **shared kernel**, not a microVM. It supports neither seccomp, AppArmor, nor capability dropping ([azure-container-apps#1232](https://github.com/microsoft/azure-container-apps/issues/1232)), and egress lockdown needs a workload-profiles environment + VNet + NAT/Firewall — `cae-codesmith-prod-centralus-001` is `Consumption` with `vnet: null`, so that is unavailable without recreating it.

The realistic risks are compute cost and outbound abuse, not container escape. Mitigations, all free:

| Control | Where |
|---|---|
| Non-root container user (`executor`, uid 10001) | `CodeSmith.Executor/Dockerfile` |
| Internal-only ingress — unreachable from the internet | `--ingress internal` below |
| System-assigned identity holding **only** AcrPull | `--system-assigned` below |
| `maxReplicas` cap bounds worst-case spend | `--max-replicas 3` below |
| One run per container (biases concurrent runs apart) | `--scale-rule-http-concurrency 1` below |
| Authenticated callers only | `[Authorize]` on `POST /api/session/{id}/run` |
| Run/compile wall-clock timeouts | `CodeSmith.Executor` `LanguageRunner` |

**Do not attach `mi-codesmith-backend-prod-centralus-001`.** That identity can reach Key Vault and SQL. Untrusted student code runs in this container and could mint tokens for it via IMDS.

## Prerequisites

Executor image already in ACR (`deploy-executor.yml` pushes `:latest` + `:<sha>`).

**Run this first, in the same shell you use for every step below.** Steps 1-4 all reference `$RG`/`$ACR`; if the shell is closed or a new tab is opened, re-run this block. An unset `$RG` fails with the misleading `argument --resource-group/-g: expected one argument`.

```powershell
$RG  = "rg-codesmith-prod-centralus-001"
$ACR = "acrcodesmithprod001"

# Confirm the variables took and you are on the right subscription
az account show --query "{name:name, id:id}" -o table
"RG=$RG  ACR=$ACR"
```

## 1. Create the Container App

```powershell
az containerapp create `
  --name ca-codesmith-exec-001 `
  --resource-group $RG `
  --environment cae-codesmith-prod-centralus-001 `
  --image "$ACR.azurecr.io/codesmith-executor:latest" `
  --system-assigned `
  --ingress internal --target-port 8080 `
  --cpu 1.0 --memory 2Gi `
  --min-replicas 0 --max-replicas 3 `
  --scale-rule-name exec-http `
  --scale-rule-type http `
  --scale-rule-http-concurrency 1
```

`--min-replicas 0` is the whole point: no replicas, no CPU/memory charge. The default 300s scale-in cooldown keeps a replica warm across repeated Test Code clicks in one session.

> First create may fail to pull if AcrPull is not yet granted (step 2). That is expected — step 3 re-points it.

Confirm the app exists and captured a principal before continuing:

```powershell
az containerapp show -n ca-codesmith-exec-001 -g $RG `
  --query "{name:name, fqdn:properties.configuration.ingress.fqdn, principal:identity.principalId}" -o json
```

## 2. Grant AcrPull to the app's own identity

```powershell
$EXEC_PRINCIPAL = az containerapp show -n ca-codesmith-exec-001 -g $RG `
  --query identity.principalId -o tsv
$ACR_ID = az acr show -n $ACR -g $RG --query id -o tsv

az role assignment create `
  --role AcrPull `
  --assignee $EXEC_PRINCIPAL `
  --scope $ACR_ID
```

## 3. Point the app at ACR via that identity

```powershell
az containerapp registry set `
  --name ca-codesmith-exec-001 `
  --resource-group $RG `
  --server "$ACR.azurecr.io" `
  --identity system
```

`deploy-executor.yml` re-runs this on every deploy (idempotent).

## 4. Point the API at the executor

```powershell
$EXEC_FQDN = az containerapp show -n ca-codesmith-exec-001 -g $RG `
  --query properties.configuration.ingress.fqdn -o tsv

az containerapp update `
  -n ca-codesmith-api-001 -g $RG `
  --set-env-vars `
    "CodeExecution__Backend=Executor" `
    "CodeExecution__Executor__BaseUrl=https://$EXEC_FQDN" `
    "CodeExecution__Executor__ExecutePath=/execute" `
    "CodeExecution__Executor__TimeoutSeconds=120"
```

The internal FQDN resolves only inside `cae-codesmith-prod-centralus-001`. No token is sent — reachability within the environment is the trust boundary.

## 5. Smoke

Prod Tutoring → **Test Code**, after a long idle so both apps are cold.

Expect up to **~2 minutes** on the first click: the API is also at `minReplicas: 0`, so a cold run pays API wake (~10-20s) *then* executor wake (60-90s for the multi-GB toolchain image). Watch the browser network tab for a client-side timeout firing before the response lands. A second run in the same session should be fast.

Spot-check one compiled language (Rust or C#) and one interpreted (Python).

## 6. Confirm it scales to zero

```powershell
# After ~30 min idle — should return an empty list
az containerapp replica list -n ca-codesmith-exec-001 -g $RG -o table
```

Then check Cost Analysis the next day.

## Troubleshooting

| Symptom | Cause |
|---|---|
| 500 `Code sandbox unavailable` | Executor cold-start exceeded 120s, or `BaseUrl` wrong. Check `az containerapp logs show -n ca-codesmith-exec-001 -g $RG`. |
| `ImagePullBackOff` on revision | AcrPull missing on the app's system-assigned principal (step 2), or `registry set` not run (step 3). |
| 401 on Test Code | Expected for unauthenticated callers — `/run` now carries `[Authorize]`. Sign in. |
| Rust/C# runs fail but Python works | Non-root toolchain paths. `RUSTUP_HOME`/`CARGO_HOME` live at `/opt/rust`; `DOTNET_CLI_HOME`/`NUGET_PACKAGES`/`GOCACHE` under `/home/executor`. |

## Rollback

`CodeExecution__Backend=Piston` is not viable in Azure (Piston needs `--privileged`, which ACA forbids). To roll back, redeploy a prior executor image sha:

```powershell
az containerapp update -n ca-codesmith-exec-001 -g $RG `
  --image "$ACR.azurecr.io/codesmith-executor:<previous-sha>"
```
