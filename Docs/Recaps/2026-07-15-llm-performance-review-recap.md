# LLM Call-Path Performance Review & Implementation

**Date:** 2026-07-15
**Type:** architecture
**Environment / Systems:** CodeSmith repo (`master`); Azure Container App `ca-codesmith-api-001`, Static Web App frontend, Azure SQL — all in `rg-codesmith-prod-centralus-001` (centralus)

## TL;DR

A `/improve-codebase-architecture` review focused on why hosted LLM operations feel slow (first request + problem generation reported 2x+ worse than local). Six candidates were grilled, prioritized, and five-plus-one implemented same-day — OTel/App Insights instrumentation, generation retry tuning, LLM transport hardening, a deepened `IUsageStore` enforcement-storage Seam (~9–13 SQL round-trips per Completion → ~6), per-input Prompt Lab submit pipelining (~2x submit speedup), and CORS preflight caching. Streaming remains the designed-but-unbuilt headline candidate; cold start is a money decision deferred (plan below). 366/366 backend tests green; context.md updated.

## Context & Goal

CodeSmith recently moved to fully hosted (Container App + SWA + Azure SQL). The user observed poor performance specifically when LLMs do work: the first request after idle, and Tutoring problem generation (noticeably 2x+ worse hosted than local). Goal: find and fix architectural sources of LLM-path latency, reviewing the entire path regardless of which symptoms were confirmed.

## Key Points Explored

- **Explore-agent latency map** of the whole call path found: no streaming anywhere (blocking full completions end-to-end); ~9–13 serialized Azure SQL round-trips per Completion in the reserve→settle enforcement lifecycle (three shallow repositories, each with its own `SaveChangesAsync`, under the per-user lock); Prompt Lab submit ≈ 70–100 serialized round-trips; problem generation up to 3 sequential Accurate-tier calls (silent truncation retries, MaxTokens 2000); unpooled DbContext; AAD-auth SQL connections; zero timing observability; frontend paying a CORS preflight per POST; sequential simulate-then-evaluate phases.
- **Transport surprises:** the Anthropic SDK defaults to a **10-minute timeout with silent transport retries**, and `Anthropic:MaxRetries: 2` in appsettings bound to nothing (dead config). A named `"Anthropic"` HttpClient with a resilience handler was registered but never consumed.
- **Grill decisions:** always-warm deferred (documented below); streaming in scope as a full candidate; OTel + App Insights chosen for observability; enforcement fix = batch per phase with **DB staying authoritative** (no in-memory balance cache — protects the multi-replica future); generation = tune now, fold into streaming later.
- **Priority reasoning:** streaming is the highest-*value* candidate (perceived latency is dominated by provider time no amount of SQL trimming touches); observability is the highest-*leverage first step* (the hosted 2x split between cold start / SQL / retries is unmeasured until traces exist).
- ⚠️ **Security find (incidental):** `appsettings.Development.json` contains live-looking Anthropic/OpenAI/xAI keys and a Stripe webhook secret, committed to the repo.

## Decisions & Outcomes

All verified by the full suite (366/366) plus a live boot smoke test (`/api/providers` 200; preflight returns `Access-Control-Max-Age: 3600`).

1. **OTel spine** — Azure Monitor distro in `Program.cs`, gated on `APPLICATIONINSIGHTS_CONNECTION_STRING`; custom `CodeSmithDiagnostics` spans: `llm.completion` → `usage.reserve`/`llm.call`/`usage.settle`|`usage.release` (provider/tier/feature/model/token tags) and `problem.generation.attempt` (attempt/truncated/parse_complete). Span behavior TDD'd via a new `ActivityCapture` listener helper.
2. **Generation tuning** — MaxTokens 2000→4000 (settle refunds to actuals, so the larger hold is free); retries now visible in traces.
3. **Transport hygiene** — both Adapters: explicit 120s timeout, zero auto-retry (a retried metered Completion double-spends provider cost); dead named client and dead config key deleted; `TimeoutSeconds`/`MaxRetries` properly bound on options.
4. **`IUsageStore` deepening** — one snapshot read (balance + IP aggregate) + ONE `SaveChangesAsync` per enforcement phase (`EfUsageStore`); single-save invariant pinned with a SaveChanges-counting interceptor test; `IIpFreeUsageRepository` deleted (failed the deletion test); balance/ledger repos remain billing-only; `AddDbContextPool` enabled; Release never materializes a missing balance row (would mint credits — new test).
5. **Prompt Lab pipelining** — `IPromptSimulator`/`IPromptEvaluator` reshaped per-input (`SimulateOneAsync`, `EvaluateOneAsync` + pure `AssembleAttempt`); orchestrator runs per-input simulate→evaluate chains in parallel. Wall clock: slowest-sim + slowest-eval → slowest single chain (~2x). A test deadlocks under sequential phases and passes only when pipelined.
6. **Edge** — CORS `SetPreflightMaxAge(1h)`.
7. **context.md** fully updated (seams table, Telemetry section, usage/credits, Prompt Lab, transport, review date 2026-07-15).

## Open Questions / Next Steps

- **App Insights Azure side (user runbook):** `Docs/Handoffs.User/2026-07-15-app-insights-azure-setup.md`. Existing workspace confirmed via screenshot: `workspace-rgcodesmithprodcentralus001pwN` — reuse it.
- **Streaming implementation (agent handoff):** `Docs/Handoffs.Agent/2026-07-15-token-streaming-handoff.md`. Locked: new shape alongside `CompleteAsync`; enforcement settles on final counts; chat streams tokens, generation streams description, Prompt Lab gets progress. Open: chunk contract, fetch-streams vs SSE (Bearer-auth constraint), decorator shape, mid-stream rollback semantics.
- **Rotate the committed secrets** in `appsettings.Development.json`; move to user-secrets.
- **Verify `VITE_API_BASE_URL` GitHub repo variable is `https://`** (couldn't check — no `gh` on the machine).
- **Deploy:** the running image predates all of this; run `deploy-azure.yml`.

### Always-warm plan (deferred by choice — do when ready to spend)

First-request latency is Container App scale-to-zero cold start (container boot + startup validation + first SQL/AAD token acquisition). When ready:

```bash
az containerapp update -n ca-codesmith-api-001 -g rg-codesmith-prod-centralus-001 \
  --min-replicas 1 --max-replicas 1
```

- Cost: one idle consumption-plan replica (0.25 vCPU / 0.5 Gi) ≈ **$10–15/mo**; idle-pricing discounts apply when the replica has no traffic.
- Keep `--max-replicas 1` until the multi-replica blockers are addressed: in-memory session stores (a session created on replica A 404s on replica B) and the in-process `UserUsageLock` (enforcement is single-instance-correct; `CreditBalance.RowVersion` is the intended cross-process guard, not yet used by the enforcer).
- Half-measures considered and rejected: external warm-ping (defeats scale-to-zero billing, still occasionally cold); scheduled warm hours (complexity > $15/mo).
- After enabling, verify cold starts are gone via the OTel request traces (first-request duration should match steady-state).

## Artifacts

- `Docs/Handoffs.Agent/2026-07-15-token-streaming-handoff.md` — ideation→implementation handoff (complete)
- `Docs/Handoffs.User/2026-07-15-app-insights-azure-setup.md` — Azure runbook (complete)
- `context.md` — updated, current
- New code: `CodeSmith.Infrastructure/Diagnostics/CodeSmithDiagnostics.cs`, `Persistence/Repositories/EfUsageStore.cs`, `CodeSmith.Core/Interfaces/IUsageStore.cs`, `Core/Models/Usage/UsageSnapshot.cs`; new tests `EfUsageStoreTests`, `ActivityCapture`; deleted `IIpFreeUsageRepository` + `EfIpFreeUsageRepository`
- Uncommitted as of writing — the working tree holds the whole change set
