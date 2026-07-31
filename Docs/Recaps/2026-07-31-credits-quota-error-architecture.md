# Credits / Quota Error Architecture

**Date:** 2026-07-31
**Type:** fix

## TL;DR

Insufficient-credits failures were correct at the enforcer (402) but broken at orchestrators and the SPA: guidance wrapped quota into generic 502 copy, Generate New failed silently, and lab submits had no error UI. We grilled a shared client-failure design, then shipped domain-exception passthrough on the backend and a SPA `interpretError` + `FailureNotice` Module (UL) wired across tutoring and both labs. Full suites green (470 backend / 187 frontend).

## Context & Goal

After free quota / paid credits are exhausted, the user saw inconsistent UX:

1. **Pair Programmer problem generation** — could show a real error on cold start.
2. **Generate New Problem** — spinner then silence; session stayed, no message.
3. **Guidance** — generic “Failed to get guidance. Please try again.” instead of a paywall.

Goal: review the architecture, decide a product-consistent approach via grill-me, and implement so every metered SPA path surfaces an honest insufficient-credits (and related) failure.

## Key Points Explored

### What already worked

- `UsageEnforcingLlmService` reserve-before-call; `InsufficientQuotaException` → **402** via `AppExceptionHandler`.
- `apiClient` / NDJSON streaming throw `ApiClientError` with `statusCode`.
- `login_required` 401 with stable ProblemDetails `code` (only machine code on metered auth).
- Problem generation path did **not** wrap quota (bubbled cleanly).

### Root causes

| Layer | Issue |
|-------|--------|
| `GuidanceConversation` | Catch wrapped everything except `AiServiceException` / cancel into `AiServiceException("Failed to get guidance…")` → **502**, including `InsufficientQuotaException`. |
| Prompt / System Lab evaluate | Catch filters omitted quota → same wrap pattern. |
| Prompt Lab start | Any generator failure (including quota) fell back to static test inputs — looked like “dynamic generation failed,” not paywall. |
| `ChatWindow` | `createSession.isError` only rendered when `!session`; in-session Generate New had no mount. |
| Lab submits | No `onError` / no error UI — pure silent fail. |
| SPA | No shared interpretation of API failures; failed-turn always appended “incomplete reply” framing even for pre-stream 402. |

### Extra inventory (beyond the three symptoms)

- CodeAnalysis-after-Test-Code uses the same metered chat path.
- Prompt Lab submit uses `Task.WhenAll` — quota can arrive as **`AggregateException`**, not bare `InsufficientQuotaException`.
- CLI dumps raw HTTP bodies; no SPA buy-credits UI yet (only billing success/cancel stubs).
- `runCode` is intentionally not metered.

### Grill-me locks (summary)

- Success bar: shared client failure + paywall **copy** (not full paywall shell / Buy Credits CTA).
- Presentation: shared `FailureNotice`; Fixed SPA copy per kind.
- Kinds: `paywall` \| `login` \| `notFound` \| `ai` \| `generic`.
- Generate New: keep current session; notice near the button.
- Chat: `FailedTurn = { failure, partial? }`; incomplete-reply sentence **only if partial is non-empty**.
- Backend: all catch-and-wrap orchestrators passthrough quota (not Guidance only).
- Surfaces: all three labs’ metered mutations + silent SPA paths (including lab submit, Prompt Lab start honesty).
- Tests: TDD both sides.

## Decisions & Outcomes

### Backend (shipped)

- **`GuidanceConversation`**: rethrow `InsufficientQuotaException` with `AiServiceException` / cancel.
- **`PromptLabService.StartChallengeAsync`**: rethrow quota; static fallback only for non-quota generation failures.
- **`PromptLabService.SubmitAttemptAsync`**: unwrap domain exceptions from `AggregateException` (parallel simulate/evaluate) so 402 is preserved.
- **`SystemLabService.SubmitAttemptAsync`**: exclude `InsufficientQuotaException` (and cancel) from evaluate wrap.

### Frontend (shipped)

- **`src/lib/clientError.ts`**: `interpretError` → `ClientFailure` (duck-typed status so tests that mock `apiClient` still work).
- **`FailureNotice`**: title + detail only (no purchase CTA this PR).
- **`StreamingChatTail`**: new `FailedTurn` shape; framing rule as locked.
- **Tutoring**: cold-start create, Generate New (`CodePanel` banner), guidance / CodeAnalysis.
- **Prompt Lab / System Lab**: start, chat, submit (notice near Submit; results panel opens on submit error).

### Verification

- `dotnet test` → **470** passed.
- `npm test` (CodeSmith.Web) → **187** passed.

### Explicitly out of scope (deferred)

- Buy Credits button / Stripe checkout from SPA.
- Account page, balance chip, proactive low-balance warnings.
- ProblemDetails `code: "insufficient_credits"`.
- Free-vs-paid reason on the 402 body.
- CLI-facing 402 polish.
- Dedicated 429 kind (maps to generic for now).

## Open Questions / Next Steps

- Wire a real **Buy Credits** CTA once thin billing UI exists (API already has checkout/balance/ledger).
- Optional: stable `insufficient_credits` extension (mirror `login_required`) and/or free-vs-paid reason codes.
- Optional: 429-specific kind and copy.
- CLI: map 402 to a clear console message *(deferred)*.
- Manual smoke with a zero-balance user across create / generate-new / guidance / lab start / lab submit *(if not already done post-merge)*.

## Artifacts

| Path | Notes |
|------|--------|
| Plan (session) | Grill + implementation plan for credits/quota error architecture |
| `CodeSmith.Infrastructure/Services/GuidanceConversation.cs` | Quota passthrough |
| `CodeSmith.Infrastructure/Services/PromptLab/PromptLabService.cs` | Start rethrow + AggregateException unwrap |
| `CodeSmith.Infrastructure/Services/SystemLab/SystemLabService.cs` | Evaluate passthrough |
| `CodeSmith.Web/src/lib/clientError.ts` | Interpretation Module (UL) |
| `CodeSmith.Web/src/features/shared/FailureNotice.tsx` | Shared presentation |
| `CodeSmith.Web/src/features/chat/components/StreamingChatTail.tsx` | FailedTurn + framing |
| `CodeSmith.Web/src/features/chat/components/ChatWindow.tsx` / `CodePanel.tsx` | Create + Generate New mounts |
| Lab windows + right panels / results panels | Start, chat, submit mounts |
| Tests | GuidanceConversation / PromptLab / SystemLab service tests; `clientError`, StreamingChatTail, ChatWindow, PromptLabWindow |
