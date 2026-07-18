# Application Insights — Azure Portal Setup

**Date:** 2026-07-18  
**Type:** ops  
**Environment / Systems:** Azure (`rg-codesmith-prod-centralus-001`, centralus); Container App `ca-codesmith-api-001`; Log Analytics `workspace-rgcodesmithprodcentralus001pwN`; App Insights `appi-codesmith-prod-001`

## TL;DR

Walked the user through Azure-side Application Insights setup in the portal (not CLI), using the existing handoff runbook. Resources and Container App secret/env wiring were completed with screenshot checkpoints. **End-to-end telemetry verification is ASSUMED successful** unless a later thread reopens `Docs/Handoffs.User/2026-07-15-app-insights-azure-setup.md` and points at this recap for failure follow-up.

## Context & Goal

Code-side OpenTelemetry → Application Insights was already merged (gated on `APPLICATIONINSIGHTS_CONNECTION_STRING`). The remaining work was manual Azure configuration the agent could not perform: create App Insights against the existing Log Analytics workspace, set the connection string on the Container App, deploy the OTel image if needed, and verify traces.

The user asked for a portal-first breakdown (what/how + steps), then executed setup live with portal screenshots for validation.

## Key Points Explored

- **Architecture:** Container App emits OTel when the connection string env var is present → Application Insights (workspace-based) → existing Log Analytics workspace. Auto-instrumentation (ASP.NET Core, HTTP, SqlClient) plus custom `CodeSmith` spans (`llm.completion` + usage phases; `problem.generation.attempt`).
- **Two independent blockers for empty telemetry:** missing env var on the Container App, and/or deployed image predating the OTel code. Both must be true for data; order does not matter.
- **Workspace confirmed in portal:** `workspace-rgcodesmithprodcentralus001pwN`, CustomerId/Workspace ID `3e51d3bb-838e-46ec-bb5f-368b28393537`, Central US, Pay-as-you-go — matches the handoff; do not create a second workspace.
- **App Insights create form validated:** name `appi-codesmith-prod-001`, RG/region correct, workspace-based to the existing LA workspace, **OTLP support Off** (correct — app uses `UseAzureMonitor()` + connection string, not OTLP ingestion).
- **Container App secret validated:** key `appinsights-conn`, type Container Apps Secret, value = App Insights connection string.
- **Portal steps 5–7 clarified:** Containers → Edit and deploy → env var `APPLICATIONINSIGHTS_CONNECTION_STRING` as secret reference to `appinsights-conn` → new revision Running; then deploy workflow + traffic + Transaction search / Application map / KQL if verifying.

## Decisions & Outcomes

- Reused existing Log Analytics workspace; created `appi-codesmith-prod-001` workspace-based in Central US.
- Connection string stored as Container App secret `appinsights-conn` and bound to process env `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- OTLP left Off on the App Insights resource.
- User reported completing secret setup (steps 1–4 of the Container App section) and was guided through env-var revision deploy (steps 5–7).

### Verification status (explicit)

**ASSUMED successful at recap time.** Full success criteria (new revision live, OTel image deployed via `deploy-azure.yml` if needed, real traffic, spans visible in Transaction search / map / KQL) were **not** re-confirmed with evidence in this thread after the final steps.

**Convention for later sessions:** If the user does **not** bring `Docs/Handoffs.User/2026-07-15-app-insights-azure-setup.md` into a new thread and point it at this recap, treat App Insights portal setup and testing as **successful**. If they do reopen that handoff against this recap, assume verification failed or is incomplete and resume from the handoff’s verify steps.

## Open Questions / Next Steps

- Optional later: dashboards / alerts (e.g. p95 on `llm.call`).
- Unrelated residual from the original performance review handoff: rotate live-looking secrets in `appsettings.Development.json` and move them to user-secrets (not part of App Insights setup).

## Artifacts

| Artifact | Role |
|----------|------|
| `Docs/Handoffs.User/2026-07-15-app-insights-azure-setup.md` | Original CLI runbook; source of resource names and verify KQL |
| `CodeSmith.Api/Program.cs` | `APPLICATIONINSIGHTS_CONNECTION_STRING` gate + `UseAzureMonitor` + `AddSource("CodeSmith")` |
| `CodeSmith.Infrastructure/Diagnostics/CodeSmithDiagnostics.cs` | Custom ActivitySource / span names |
| `context.md` → Telemetry section | Span/tag reference |
| `.github/workflows/deploy-azure.yml` | Manual deploy of image with OTel code |
| Azure: `appi-codesmith-prod-001` | App Insights resource (created this thread) |
| Azure: secret `appinsights-conn` + env on `ca-codesmith-api-001` | Connection string wiring |
|
