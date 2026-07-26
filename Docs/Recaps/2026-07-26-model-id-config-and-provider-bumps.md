# Model ID Configuration and Provider Model Bumps

**Date:** 2026-07-26
**Type:** implementation
**Environment / Systems:** CodeSmith.Api (Azure Container Apps `ca-codesmith-api-001`), `LlmPricingCatalog`, usage/credits seam

## TL;DR

Investigated decoupling model selection from provider selection so new model releases wouldn't require a code change. Found that model IDs were *already* config-driven — the real coupling was the hard-coded `LlmPricingCatalog` rate table gated by startup validation. Decided to keep the table in code (it's a billing input), added a CI guard so drift fails a test instead of a production boot, then bumped xAI to `grok-4.5` and Anthropic to `claude-sonnet-5` / `claude-haiku-4-5`.

## Context & Goal

Starting complaint: "grok 4.3 is still used even though xAI is on 4.5 now," with the stated goal of defining only the *provider* in config and letting the backend resolve the newest model automatically. Ran as a `/grill-me` design interrogation before touching code.

## Key Points Explored

- **Premise correction.** Model IDs were never hard-coded in the backend. `appsettings.json` → `XaiOptions.AccurateModel` / `FastModel` already drove them; the values in the C# options classes are just defaults. `AnthropicService.cs:48` maps `ModelTier.Fast|Accurate` → the configured name.
- **The actual coupling** was `LlmPricingCatalog.cs` — an `internal static` rate table, with `ServiceCollectionExtensions.cs:210-216` running `ValidateOnStart()` against it. A model with no rate entry refuses the boot. So a model bump required editing C# + redeploying.
- **"Newest model of the family" rejected.** Three reasons: `/v1/models` is flat and undifferentiated (a `grok-4.5-fast` shipping after `grok-4.5` would silently downgrade the Accurate tier); no provider publishes prices via API, so auto-adoption means serving traffic at the old rate while paying the new one; and prompt-tuned surfaces (PromptLab rubric scoring, SystemLab eval) would shift with nothing in git to blame.
- **Blast-radius analysis of moving the rate table to config.** Traced the billing path and confirmed it would *not* break anything: `SettleAsync` (`UsageEnforcer.cs:190-201`) writes `CostUsd` / `ProviderCostUsd` as literal decimals into an append-only ledger and never re-reads the table; free quota is denominated in tokens (`FreeTokensUsedInWindow`, `IpFreeTokenCap` = 60,000), not dollars; and `EstimateUpperBoundCost` (`LlmPricing.cs:54-61`) uses only the global `HighestRatePer1K` ceiling.
- **Azure picture.** Container Apps env vars already carry config (`Xai__ApiKey`, `ConnectionStrings__CodeSmithDb`). An env-var change spins a new revision from the same image — ~30s, no ACR push, no CI run. No structural change would have been needed.
- **grok-4.5 has tiered pricing** — $2/$6 per MTok at ≤200K context, $4/$12 above it. The catalog key is `(Provider, Model)` with a single flat rate pair and has no context dimension.
- **Anthropic model data** pulled from the loaded model catalog rather than a web search. Sonnet 5 carries an introductory $2/$10 per MTok through 2026-08-31 vs. a standard $3/$15.

## Decisions & Outcomes

- **Rate table stays in code.** Reversed an earlier recommendation to move it to config. It's a billing input on a system that debits purchased credits, so it earns code review (a `0.02` vs `0.002` typo caught in a PR diff, not by a customer), test coverage, and git history. The payoff — saving ~10 min a few times a year — didn't justify losing those.
- **Added a CI guard.** `ProviderOptionsValidationTests.ShippedAppSettings_ConfiguredModels_ArePricedInCatalog` loads the real `appsettings.json` from the test output directory (it flows transitively via the `CodeSmith.Api` ProjectReference — no csproj change) and runs the same `AddCodeSmithInfrastructure` + `IOptions<T>.Value` validation the host runs. Verified red-then-green using the actual grok-4.5 bump. `appsettings.Development.json` is deliberately **not** layered: `.gitignore:381` excludes it, so layering would make the guard assert less on CI than locally.
- **xAI → `grok-4.5`.** Encoded the **≤200K rate** (0.002 / 0.006) because `editorContent` caps at 50,000 chars (~12.5K tokens) — an order of magnitude below the cliff. Above 200K the 2.0× markup exactly absorbs the 2× rate jump, so margin falls to zero but never negative; commented in the table. `ContextWindow` 1,000,000 → 500,000.
- **Anthropic → `claude-sonnet-5` / `claude-haiku-4-5`.** Haiku was an alias swap (4.5 is still current, undated alias preferred); Sonnet was a real generation bump at an unchanged 0.003 / 0.015.
- **Encoded Sonnet 5's standard $3/$15, not the intro $2/$10.** Encoding the promotional rate would start silently undercharging on 2026-09-01 with no trigger to fix it. Over-recovering ~33% on Anthropic traffic for five weeks is the safe direction.
- **Context window gap accepted.** Sonnet 5 (1M) and Haiku 4.5 (200K) no longer share a window, but `AnthropicOptions` has one `ContextWindow` field. Left at 200,000 so the frontend `TokenUsageBar` under-reports headroom rather than over-reports; documented in the options class.
- **`grok-4.3` removed** from the catalog at the user's direction, along with every test reference.
- Full suite green: **424 passed**.

## Open Questions / Next Steps

- **OpenAI still on `gpt-4.1` / `gpt-4.1-mini`.** Not bumped — the web search returned a GPT-5.6 Sol/Terra/Luna family from aggregators only, and a wrong model ID is a boot failure. Waiting on the user's own OpenAI pricing page.
- **`HighestRatePer1K = 0.015` is now load-bearing and untested.** It's the pre-call reserve ceiling, and `SettleAsync` (`UsageEnforcer.cs:188`) does not floor `PaidCreditsBalance` at zero — a model whose output rate exceeds 0.015/1K lets the gate under-reserve and a balance go negative. Sonnet 5 sits exactly on the line at 0.015. Reported OpenAI Sol (~0.030) would be 2× over. The user declined a CI assertion for this, so it stays a manual check.
- **Detection gap unresolved.** Nothing in the codebase tells you a provider shipped a newer model — the original complaint. Options discussed but not chosen: daily `IHostedService` check against each provider's `/v1/models`, startup-only check, or an admin diagnostic endpoint.
- **Not yet deployed.** Requires a `deploy-azure.yml` run (`appsettings.json` is baked into the image). Confirm first that no `Xai__AccurateModel` / `Anthropic__AccurateModel` env var override exists on the Container App — it would win over the edited file.
- **Free-tier cost shift.** grok-4.5 output is 2.4× grok-4.3. `FreeMonthlyTokenQuota` and `IpFreeTokenCap` were sized against the old rate.

## Artifacts

Production code:
- `CodeSmith.Infrastructure/Services/Usage/LlmPricingCatalog.cs` — grok-4.5 added w/ tiered-pricing comment, grok-4.3 removed, Anthropic keys renamed
- `CodeSmith.Infrastructure/Configuration/XaiOptions.cs` — `grok-4.5`, `ContextWindow` 500,000
- `CodeSmith.Infrastructure/Configuration/AnthropicOptions.cs` — `claude-sonnet-5` / `claude-haiku-4-5`, context-window gap documented
- `CodeSmith.Api/appsettings.json` — both provider blocks

Tests:
- `CodeSmith.Tests/Infrastructure/ProviderOptionsValidationTests.cs` — new shipped-config guard + `IConfiguration` overload of `BuildProvider`
- `LlmPricingCatalogTests.cs`, `LlmPricingTests.cs`, `AnthropicLlmServiceTests.cs`, `UsageEnforcerTests.cs`, `UsageEnforcingLlmServiceTests.cs` — model-ID updates

Docs:
- `context.md:167` — CI guard documented alongside the `ValidateOnStart` note; `:250-251,257` — model/context-window facts
- `Docs/Recaps/2026-06-28-xai-default-pricing-markup.md` — left unedited as a dated point-in-time record
