# Token Streaming Seam — Thread Handoff Document

> **Handoff Mode: Ideation → Implementation**
> **Receiving agent job: Pressure-test this design, then implement**

---

## 1. Thread Purpose

A performance-focused architecture review of CodeSmith's LLM call path identified streaming as the highest-value remaining candidate: every LLM call today is a blocking request→full-response cycle, so users stare at a dead spinner for 10–30s per Completion. This handoff carries the locked design decisions for adding a **streaming shape to the LLM Completion Seam** so time-to-first-token becomes the perceived latency. The rest of the review (observability, enforcement batching, transport hygiene, Prompt Lab pipelining) is **already implemented and merged** — streaming is the only unbuilt candidate.

## 2. Stack & Environment

- **Backend:** .NET 8, ASP.NET Core Web API (`CodeSmith.Api`, HTTP 5175 / HTTPS 7111)
- **LLM SDKs:** `Anthropic` 12.x (official), `OpenAI` 2.x (also drives xAI/Grok via `https://api.x.ai/v1`); active provider default is **Xai** (`grok-4.3` both tiers)
- **Frontend:** React 19 + TypeScript + Vite 6, TanStack Query v5, native `fetch` via `src/lib/apiClient.ts` (no axios)
- **Hosting:** Azure Container App `ca-codesmith-api-001` (rg `rg-codesmith-prod-centralus-001`, centralus); SPA on Azure Static Web Apps calling the API **cross-origin** with a baked `VITE_API_BASE_URL`
- **Auth:** Entra Bearer on LLM-mutating endpoints; Development adds an `X-Debug-User-Id` scheme
- **Tests:** xUnit + NSubstitute (backend), Vitest + RTL (frontend); TDD is the project default
- **Architecture reference:** `context.md` at repo root is ground truth (seams table, Ubiquitous Language). Use its vocabulary exactly: Module, Interface, Seam, Adapter, Completion, ModelTier, Feature, Guidance Turn.

## 3B. Full Specification (locked decisions)

### Scope shape

1. **Streaming is a NEW operation shape on the existing LLM Completion Seam, alongside `CompleteAsync` — not a replacement.** Non-streaming callers (Prompt Lab simulate/evaluate, System Lab evaluation, test-input generation, problem parsing retry loop) keep calling `CompleteAsync` unchanged.
2. **Usage enforcement is untouched in shape.** The reserve → settle / release lifecycle stays exactly as-is: `ReserveAsync` before the stream opens, `SettleAsync` on the **final** token counts once the stream completes, `ReleaseAsync` if the stream fails having produced nothing billable. A final `LlmResponse` (with real `InputTokensUsed`/`OutputTokensUsed`) must still exist at stream end — both SDKs report usage in their stream-final events.
3. **Per-surface behavior:**
   - **Guidance Turns (all three surfaces' chat):** stream raw assistant text token-by-token to the browser.
   - **Problem generation (Tutoring session create):** stream the `DESCRIPTION:` portion into the UI as it is written; the starter code fills the Monaco editor only at parse-complete (the `DESCRIPTION:` / `STARTER_CODE:` marker format needs the full text to parse reliably).
   - **Prompt Lab submit:** **no token streaming.** It is 8 parallel scored Completions; stream *progress* instead (e.g., "3/4 inputs simulated", "2/4 evaluated") if anything. Lowest priority of the three.
4. **Transport to the browser:** server-sent event stream over HTTP (the API writes chunks; the SPA consumes incrementally). Whether that is literal SSE (`text/event-stream`) or a `fetch` readable-stream body is an **open decision** — see §4B; note the auth constraint there before choosing.

### Existing seams the work lands on (all current, post-review state)

| Piece | File | Relevance |
|---|---|---|
| `ILlmService.CompleteAsync(CompletionRequest, ct)` | `CodeSmith.Core/Interfaces` | The seam gaining a streaming sibling |
| `AnthropicLlmService` | `CodeSmith.Infrastructure/Services/AnthropicService.cs` | Adapter; singleton; explicit 120s timeout, `MaxRetries = 0`; internal `HttpClient` test seam |
| `OpenAiCompatibleLlmService` | `CodeSmith.Infrastructure/Services/OpenAiCompatibleLlmService.cs` | One adapter for OpenAI + xAI; `NetworkTimeout` 120s, `ClientRetryPolicy(0)` |
| `UsageEnforcingLlmService` | `Services/Usage/Decorators/` | Scoped decorator running reserve → call → settle/release; emits `llm.completion`/`usage.*`/`llm.call` OTel spans (source `CodeSmithDiagnostics`, `"CodeSmith"`). The streaming path must be decorated the same way. |
| `IGuidanceConversation.RunTurnAsync` | `Services/GuidanceConversation.cs` | Owns the turn invariant: append user msg → trim window → one Fast Completion → append reply → persist; **rolls the user turn back on failure**. Streaming must preserve this invariant — see §4B. |
| `ILlmServiceFactory.Get(provider)` | keyed DI | Runtime provider routing; two-layer registration (raw singleton adapter + scoped enforcing decorator) |
| Frontend chat hooks | `CodeSmith.Web/src/features/*/hooks/` | All server calls are TanStack Query mutations today; one blocking `fetch` per action via `apiClient.ts` |

### Constraints that shape the design

- **Sessions are in-memory and single-replica**; per-session mutation is serialized by `WithSessionLockAsync`. A streaming turn holds its session context the same way a blocking one does — do not persist partial turns.
- **Rate limiter:** fixed window 60 req/min per client IP; a long-lived stream is one request — fine — but confirm the limiter isn't counting SSE keep-alives.
- **Transport hygiene already set:** no SDK auto-retry (a retried half-delivered stream would double-bill and garble output — this is load-bearing for streaming), 120s total-call timeout (may need rethinking as a per-chunk idle timeout for streams; open question).
- **OTel spans exist** (`llm.completion` → `usage.reserve`/`llm.call`/`usage.settle|release`): extend `llm.call` with time-to-first-token when streaming lands — the instrumentation was built partly for this.
- **Tier downgrade:** the enforcing decorator rewrites evaluation features to Fast tier while on free quota (`EffectiveRequest`). Guidance/generation aren't evaluation features, but the decorator wrapping must still apply to the streaming path.

## 4B. What Is NOT Yet Decided

1. **Chunk contract** — shape of the streamed unit (raw text delta only? or typed events: `delta`, `progress`, `final` with token counts / `contextTokensUsed` for the TokenUsageBar?). The final event almost certainly needs the `ChatResponse` metadata the UI shows today.
2. **SSE vs fetch readable-stream** — key constraint: `EventSource` cannot send an `Authorization: Bearer` header; chat endpoints are `[Authorize]`. A `fetch` POST with streamed response body avoids that entirely and fits the existing `apiClient.ts` idiom; literal SSE would need token-in-query or a different auth dance. Lean fetch-streams, but decide explicitly.
3. **Streaming method shape on the seam** — e.g., `IAsyncEnumerable<CompletionChunk> StreamAsync(CompletionRequest, ct)` where the final chunk carries the `LlmResponse`, vs. a callback/channel shape. Must keep the enforcing decorator able to observe the final usage without buffering the whole stream.
4. **`IGuidanceConversation` partial-delivery semantics** — if the stream dies mid-reply: roll back the user turn (current invariant) and discard the partial assistant text? Or persist nothing but leave the partial text visible client-side with an error banner? History must never contain a partial assistant message (provider rejects malformed alternation).
5. **`WasTruncated` mid-stream** — truncation is only knowable at stream end (`max_tokens` stop reason); generation's retry loop reads it. Streaming generation + retry-on-truncation interact: does a retry restart the visible stream?
6. **Idle/total timeout for streams** — 120s total may be right; consider a per-chunk idle timeout instead so a stalled provider fails fast without capping long healthy streams.
7. **Frontend consumption pattern** — TanStack Query mutations don't model incremental data; likely a thin custom hook around `fetch` + `ReadableStream` with component state for the accumulating text, mutation semantics preserved for completion/error. Must stay within the "all API calls via apiClient.ts" convention.
8. **CORS/proxy behavior** — response streaming through Container Apps ingress + cross-origin fetch: verify no buffering (e.g., response flushing, `X-Accel-Buffering`-style issues) in the hosted path early, with a walking-skeleton spike.

## 5. Key Decisions & Rationale

| Decision | Rationale |
|---|---|
| Streaming added alongside `CompleteAsync`, not replacing it | 5+ callers are single-shot JSON-scored calls that gain nothing from streaming; forcing them through a stream shape shallows the seam |
| Enforcement settles on final counts; lifecycle unchanged | Reserve→settle already reconciles an upper-bound hold to actuals; a stream just delays the actuals. No cross-seam redesign needed |
| Chat streams tokens; generation streams description only; Prompt Lab gets progress not tokens | Matches what each surface's output *is*: raw prose, semi-structured parse target, and a scored batch respectively |
| No SDK auto-retry (already implemented) | A transport retry re-runs a metered Completion — double provider cost, garbled half-streams |
| OTel spans first (already implemented) | Time-to-first-token and stream-duration need a home; debugging mid-stream failures needs dependency traces |
| xAI is the default provider | Both adapters must stream; OpenAI-compatible streaming covers xAI + OpenAI, Anthropic SDK streaming covers Anthropic |

## 7. Next Steps (Ordered)

1. **Pressure-test the spec in §3B before writing any code.** Challenge assumptions, surface contradictions, identify missing edge cases — especially §4B items 2–4 (auth/transport, decorator shape, rollback semantics). The user expects to be grilled (`/grill-me` style, one question at a time, options with a recommended answer).
2. Resolve §4B decisions with the user; record them (context.md gets the vocabulary; an ADR if anything overturns a locked decision in §5 — locked decisions are not re-litigated without one).
3. Walking skeleton: one hard-coded streaming endpoint through the full hosted path (Container App ingress + cross-origin fetch from the SWA) to de-risk §4B item 8 before building the seam properly.
4. TDD the seam bottom-up: adapter streaming (HTTP-level tests with the existing `CapturingHttpHandler` idiom against SSE fixture bodies) → enforcing decorator over the stream (reserve/settle/release + span assertions via `ActivityCapture`) → `IGuidanceConversation` turn invariant → API endpoint → frontend hook.
5. Update `context.md` (seams table, Guidance Conversation section, new UL terms like `CompletionChunk`) as decisions crystallize — not at the end.

## 8. Must-Knows for the New Thread

- **User conventions (non-negotiable):** TDD; `// == Block Title == //` comments; `/// <summary>` only at class/interface level, inline `//` for members; edit existing files over creating new ones; explain what/why/effect when overwriting a file; no affirmations; direct pushback expected; plan mode = zero file changes; implementation requires explicit approval — a stated preference is not a green light.
- **Ubiquitous Language is enforced** — say Seam/Adapter/Module/Interface/Completion/Guidance Turn, mark them (UL) when drawing from the list; never "boundary/component/service."
- **The interface is the test surface.** When the seam gains the streaming shape, test through it; if a Module is deepened, delete the old shallow tests rather than layering.
- Span-emitting test classes must join the `CodeSmithTelemetry` xUnit collection (ActivityListeners are process-global).
- `LlmResponse.Model` is always stamped with the **configured** model name, never the served one — pricing-catalog invariant; preserve in the streaming path.
- 402 = quota exhausted, 429 = rate-limited, 499 = caller cancelled (OCE with cancelled token), 502 = provider failure — mapped declaratively in `AppExceptionHandler`. A mid-stream failure after headers are sent cannot change the status code; the chunk contract needs an error event shape.
- Live API keys were found committed in `appsettings.Development.json` — flagged for rotation in the recap doc; don't be surprised if keys have rotated when testing.

## 9. Relevant Artifacts

- `context.md` (repo root) — authoritative architecture reference, current as of 2026-07-15 (includes this review's changes). **Read first.**
- `Docs/Recaps/2026-07-15-llm-performance-review-recap.md` — full recap of the performance review this handoff came from.
- `CodeSmith.Infrastructure/Diagnostics/CodeSmithDiagnostics.cs` — ActivitySource for the spans streaming should extend (complete).
- `CodeSmith.Tests/Infrastructure/ActivityCapture.cs` — span-assertion test helper (complete).
- `CodeSmith.Tests/Infrastructure/{AnthropicLlmServiceTests,OpenAiCompatibleLlmServiceTests}.cs` — the HTTP-seam adapter test idiom to extend for stream fixtures (complete).
- No streaming code exists anywhere — backend or frontend. This is greenfield on an existing seam.

---

> **Paste into new thread:**
> "Here's a fully-specified feature design from an ideation session. Your job is to pressure-test this spec first — challenge assumptions, surface gaps, identify contradictions — then implement it once we've aligned. Here's the spec: [paste document]
> Start by grilling the design before writing any code."
