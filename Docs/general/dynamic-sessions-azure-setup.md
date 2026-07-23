# == Azure setup: Custom Dynamic Sessions for code execution == #

One-time operations to host CodeSmith's multi-language executor in Azure Container Apps
**custom Dynamic Sessions** (Hyper-V sandboxes). Complements `deploy-executor.yml`.

## Prerequisites

- Resource group: `rg-codesmith-prod-centralus-001`
- Container Apps environment: `cae-codesmith-prod-centralus-001`
- ACR: `acrcodesmithprod001`
- Managed identity: `mi-codesmith-backend-prod-centralus-001` (API app uses this)
- Executor image pushed: `acrcodesmithprod001.azurecr.io/codesmith-executor:<tag>`

## 1. Create session pool (custom container)

Adjust identity resource IDs to match your subscription.

```bash
# Example — verify exact az containerapp sessionpool flags for your CLI version
az containerapp sessionpool create \
  --name sp-codesmith-exec-001 \
  --resource-group rg-codesmith-prod-centralus-001 \
  --environment cae-codesmith-prod-centralus-001 \
  --container-type CustomContainer \
  --image acrcodesmithprod001.azurecr.io/codesmith-executor:latest \
  --cpu 1.0 \
  --memory 2Gi \
  --target-port 8080 \
  --ready-sessions 0 \
  --max-sessions 3 \
  --cooldown-period 300 \
  --network-status EgressDisabled \
  --registry-server acrcodesmithprod001.azurecr.io \
  --mi-user-assigned /subscriptions/<SUB>/resourceGroups/rg-codesmith-prod-centralus-001/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mi-codesmith-backend-prod-centralus-001
```

If your CLI version uses different parameter names, use the portal or ARM/Bicep with:

- `readySessionInstances: 0` (scale-to-zero / no idle bill for warm pods)
- `maxConcurrentSessions: 3`
- `sessionNetworkConfiguration.status: EgressDisabled`
- Ingress target port **8080**
- Probes: `GET /health` (liveness), `GET /ready` (startup) on port 8080

## 2. Grant Session Executor to the API identity

```bash
POOL_ID=$(az containerapp sessionpool show \
  --name sp-codesmith-exec-001 \
  --resource-group rg-codesmith-prod-centralus-001 \
  --query id -o tsv)

PRINCIPAL_ID=$(az identity show \
  --name mi-codesmith-backend-prod-centralus-001 \
  --resource-group rg-codesmith-prod-centralus-001 \
  --query principalId -o tsv)

az role assignment create \
  --role "Azure ContainerApps Session Executor" \
  --assignee "$PRINCIPAL_ID" \
  --scope "$POOL_ID"
```

## 3. Point the API Container App at Dynamic Sessions

Get the pool management endpoint:

```bash
az containerapp sessionpool show \
  --name sp-codesmith-exec-001 \
  --resource-group rg-codesmith-prod-centralus-001 \
  --query properties.poolManagementEndpoint -o tsv
```

Set on `ca-codesmith-api-001` (env vars or Key Vault refs):

| Name | Value |
|------|--------|
| `CodeExecution__Backend` | `DynamicSessions` |
| `CodeExecution__DynamicSessions__PoolManagementEndpoint` | *(from show above)* |
| `CodeExecution__DynamicSessions__ExecutePath` | `/execute` |
| `CodeExecution__DynamicSessions__TimeoutSeconds` | `120` |

Ensure the API app uses the same user-assigned MI for outbound Entra tokens
(`DefaultAzureCredential` resolves it when `AZURE_CLIENT_ID` is set if multiple MIs).

## 4. Ingress timeout on the API app

Cold start of a multi-lang session image can approach 60–90s. Raise the API Container App
request timeout if the platform default is lower than `TimeoutSeconds` (120).

## 5. Smoke test

1. Open CodeSmith (prod), create a Tutoring session.
2. Press **Test Code** once — expect possible “Starting sandbox…” after 5s.
3. Press **Test Code** again in the same session — should be much faster (same pool identifier).
4. Spot-check each language once.

## Local development

Unchanged: `CodeExecution:Backend=Piston` + `docker compose up -d piston`.
DynamicSessions is production-only unless you configure a pool endpoint locally with `az login`.
