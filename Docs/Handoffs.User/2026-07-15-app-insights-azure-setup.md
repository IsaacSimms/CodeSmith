# Application Insights — Azure-Side Setup (User Runbook)

> **Handoff Mode: Implementation**
> **Receiving agent job: Resume and continue — executed by the USER in the Azure CLI, not by an agent.** Every code-side piece is done and merged; this document is only the manual Azure work.

---

## 1. Thread Purpose

The 2026-07-15 performance review wired OpenTelemetry into the CodeSmith API (Azure Monitor distro + custom `CodeSmith` spans). The code activates **only** when the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable is present. This runbook creates the Azure resources, sets that variable on the Container App, and verifies telemetry — the last mile the agent could not do (no `az`/`gh` access from the session).

## 2. Environment

- Resource group: `rg-codesmith-prod-centralus-001` (centralus)
- Container App: `ca-codesmith-api-001`
- **Log Analytics workspace already exists** (confirmed via screenshot on 2026-07-15): `workspace-rgcodesmithprodcentralus001pwN`, centralus, retention 30 days, CustomerId `3e51d3bb-838e-46ec-bb5f-368b28393537`. **Do not create a new workspace — reuse this one.** (Verify the exact name first; the screenshot column may be truncated: `az monitor log-analytics workspace list -g rg-codesmith-prod-centralus-001 -o table`.)
- Deploy workflow: `.github/workflows/deploy-azure.yml` (manual `workflow_dispatch`; pushes image to ACR `acrcodesmithprod001` and updates the Container App)

## 3. What Was Accomplished (code side — already done)

- `Azure.Monitor.OpenTelemetry.AspNetCore` added to `CodeSmith.Api`; wired in `Program.cs` behind the `APPLICATIONINSIGHTS_CONNECTION_STRING` check. Auto-instruments inbound requests, outbound HTTP (LLM provider calls), and SqlClient (usage-enforcement round-trips).
- Custom spans emitted from `CodeSmithDiagnostics` (source `"CodeSmith"`): `llm.completion` (+ `usage.reserve` / `llm.call` / `usage.settle` / `usage.release` children, tagged with provider/tier/feature/model/tokens) and `problem.generation.attempt` (tagged attempt/truncated/parse_complete).
- All of this is on `master` — but **the image currently deployed predates it**.

## 4. Current State

Telemetry emits nothing yet, for two independent reasons: the env var is not set on the Container App, and the running image doesn't contain the OTel code. Both must be fixed (order doesn't matter; traffic after both = data).

## 7. Next Steps (Ordered) — the actual runbook

**Step 1 — one-time CLI extension:**
```bash
az extension add --name application-insights
```

**Step 2 — create the App Insights resource against the EXISTING workspace:**
```bash
az monitor app-insights component create \
  -g rg-codesmith-prod-centralus-001 \
  -a appi-codesmith-prod-001 \
  -l centralus \
  --workspace workspace-rgcodesmithprodcentralus001pwN
```
(If the workspace name errors, re-run the `workspace list` command from §2 and paste the exact name — the screenshot may have truncated it.)

**Step 3 — get the connection string:**
```bash
az monitor app-insights component show \
  -g rg-codesmith-prod-centralus-001 \
  -a appi-codesmith-prod-001 \
  --query connectionString -o tsv
```

**Step 4 — set it on the Container App (secret + env-var-from-secret):**
```bash
az containerapp secret set \
  -n ca-codesmith-api-001 \
  -g rg-codesmith-prod-centralus-001 \
  --secrets appinsights-conn="<connection string from step 3>"

az containerapp update \
  -n ca-codesmith-api-001 \
  -g rg-codesmith-prod-centralus-001 \
  --set-env-vars APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-conn
```
The `update` creates a new revision — expected and fine.

**Step 5 — deploy the current build:** run the `deploy-azure.yml` workflow (GitHub → Actions → Deploy → Run workflow) so the image with the OTel code is what's running.

**Step 6 — generate traffic:** open the app, create a Tutoring session, send a chat turn, submit a Prompt Lab attempt. Allow 1–3 minutes for ingestion.

**Step 7 — verify.** Portal → `appi-codesmith-prod-001`:
- *Transaction search* → open a `POST /api/session` request → waterfall should show `llm.completion` with `usage.reserve` → `llm.call` → `usage.settle` children plus SQL dependencies. This view answers "where does the hosted latency go."
- *Application map* → edges to `api.x.ai` and Azure SQL with average latencies.
- *Logs (KQL)*:
```kusto
// Provider time vs enforcement time per Completion
dependencies
| where timestamp > ago(1d)
| where name in ("llm.call", "usage.reserve", "usage.settle")
| summarize avg(duration), percentile(duration, 95), count() by name

// Are problem-generation retries firing?
dependencies
| where name == "problem.generation.attempt"
| extend attempt = tostring(customDimensions["codesmith.attempt"]),
         truncated = tostring(customDimensions["codesmith.truncated"])
| summarize count() by attempt, truncated
```
Custom tags (`codesmith.provider`, `codesmith.tier`, `codesmith.feature`, token counts) live in `customDimensions`.

## 8. Must-Knows

- **Cost:** effectively free at current traffic. Log Analytics ingestion ≈ $2.30–2.75/GB after the free grant; this app emits megabytes. The workspace's 30-day retention is already the cheap setting. If cost ever appears, cap with a daily quota on the *workspace* — don't touch the code.
- The Azure Monitor distro samples adaptively by default — leave as-is.
- Local dev is unaffected: without the env var, `StartActivity` has no listener and the instrumentation costs nothing.
- No dashboards/alerts were configured — optional later step (e.g., alert on `llm.call` p95).
- Related but separate flag from the same review: **`appsettings.Development.json` contains live-looking API keys and a Stripe webhook secret — rotate them and move to user-secrets.** Not part of this runbook, but do it the same sitting.

## 9. Relevant Artifacts

- `CodeSmith.Api/Program.cs` — the `APPLICATIONINSIGHTS_CONNECTION_STRING` gate + `UseAzureMonitor().WithTracing(AddSource("CodeSmith"))` (complete).
- `CodeSmith.Infrastructure/Diagnostics/CodeSmithDiagnostics.cs` — the ActivitySource (complete).
- `context.md` → "Telemetry (OpenTelemetry → Application Insights)" section — reference for span names/tags (current).

---

> **Paste into new thread (if resuming with an agent instead of running manually):**
> "Picking up from a previous session. Here's the handoff: [paste document]
> Confirm you have context and flag anything unclear before we continue."
