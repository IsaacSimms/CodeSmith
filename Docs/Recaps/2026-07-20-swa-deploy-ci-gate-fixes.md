# Azure SWA Deploy CI Gate Fixes

**Date:** 2026-07-20
**Type:** fix
**Environment / Systems:** GitHub Actions (`Deploy Static Web App`), Azure Static Web Apps, CodeSmith.Web (Vite + Vitest + TypeScript)

## TL;DR

The Azure SWA deploy workflow was failing on two sequential gates: a TypeScript strictness error in a test file during `tsc -b`, then unit tests that expected relative `/api` URLs while CI injected production `VITE_API_BASE_URL`. Both are fixed; deploy build/test gates are tighter and hermetic.

## Context & Goal

Manual `workflow_dispatch` of **Deploy Static Web App** (`deploy-swa.yml`) failed before the Azure upload step. Goal: keep deploy as a real sanity gate (typecheck + tests + production Vite env for the SPA bundle), fix what blocked it, and record the root causes.

## Key Points Explored

### Failure 1 — TypeScript on build (`TS2532`)

- Step: **Install and build** → `npm run build` → `tsc -b && vite build`
- Error: `src/auth/AuthControls.test.tsx(69,12): error TS2532: Object is possibly 'undefined'.`
- Cause: `loginRedirect.mock.calls[0][0]` under `noUncheckedIndexedAccess` in `tsconfig.test.json`. Root `tsconfig.json` project-references app **and** test configs, so deploy typechecks tests.
- Local: `npm test` (Vitest) passed; `tsc -b` failed — different tools (runtime scenario vs type soundness). Vitest does not re-run the full `tsc -b` graph.

### Failure 2 — Vitest after tests were added to the gate

- After the TS fix, build passed; **7 failures** in `src/lib/apiClient.test.ts`.
- Tests expected relative paths (`/api/session`, …). CI received absolute URLs:
  `https://ca-codesmith-api-001....azurecontainerapps.io/api/...`
- Cause: deploy job set `VITE_API_BASE_URL` (and other `VITE_*` vars) on the same step that ran `npm test`. `resolveApiUrl` prefixes that base when set. Locally the var was unset → relative paths → green.
- Confirmed product code path is intentional (relative in dev, absolute when base is set); the leak was workflow/test isolation, not wrong `resolveApiUrl` logic.

### Deploy gate design

- Keep typechecking tests on `npm run build` (`tsc -b` over all project references).
- Also run unit tests on deploy as a hard gate.
- Scope production Vite env to the **build** step only so the SPA bakes real API/AAD config without poisoning tests.

## Decisions & Outcomes

| Decision | Outcome |
|----------|---------|
| Fix TS2532 with safe narrowing | `firstArgs = mock.calls[0]?.[0]` + `toBeDefined()` then property assert |
| Keep test typecheck in build | No change to `tsc -b` graph; still a deploy guardrail |
| Run `npm test` on deploy | Workflow runs install → test → build (with `VITE_*`) → SWA deploy |
| Hermetic API client tests | `beforeEach` stubs `VITE_API_BASE_URL` to `""` so ambient CI/local env cannot leak into default URL asserts |
| Scope Vite env to Build step | Test step has no production `VITE_*`; Build still receives GitHub vars for the bundle |

**Verification:** Local `npm run build` + `npm test` green; tests also green when `VITE_API_BASE_URL` is set to the real Azure API host (simulating the old CI leak). User confirmed the workflow issue is resolved after push/re-run.

## Open Questions / Next Steps

- Node 20 deprecation warning on some Actions (checkout forcing Node 20 while job uses Node 22) — noise; optional cleanup later.
- No PR-level CI workflow was added; only the manual SWA deploy path was hardened *(inferred optional follow-up)*.

## Artifacts

| Path | Change |
|------|--------|
| `CodeSmith.Web/src/auth/AuthControls.test.tsx` | Safe mock-call narrowing for `extraQueryParameters` assert |
| `CodeSmith.Web/src/lib/apiClient.test.ts` | Hermetic empty `VITE_API_BASE_URL` in `beforeEach` |
| `.github/workflows/deploy-swa.yml` | Split into Install → Test → Build (env on Build only) → Deploy |

**Reproduce / verify (PowerShell):**

```powershell
cd C:\CodeSmith\CodeSmith\CodeSmith.Web
npm ci
npm test
npm run build
```
