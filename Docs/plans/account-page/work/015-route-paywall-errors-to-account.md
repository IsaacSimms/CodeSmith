---
id: 015
title: Route paywall and login failures to the account page
status: todo
implements: [005]
depends_on: [007]
---

## Goal

Give a 402 or a 401 somewhere to send the user, now that a page exists to send them to.

## Constraints

- `ClientFailure` gains an optional `action: { label, href }`; `interpretError` populates it for
  `paywall` (→ `/account#credits`) and for `login`; `FailureNotice` renders it when present and
  otherwise stays presentational — map constraint 10
- Two real consumers, so this is a real seam — one adapter would have been hypothetical
- This reverses the deliberate "no CTA" note at `FailureNotice.tsx:9`, written when there was
  nowhere to send the user. Update that comment rather than leaving it contradicting the code.
- The `#credits` target only works because
  [Build the account page shell](007-build-account-page-shell.md) implements hash arrival explicitly
- Metered auth failures are 401 `login_required`; exhausted quota and credits are 402 — see
  `CLAUDE.md` → API Endpoints

## Acceptance criteria

- A 402 from any metered endpoint renders a `FailureNotice` with a CTA that navigates to
  `/account#credits`, landing on the credits card with its ring.
- A 401 `login_required` renders its own CTA.
- A test proves failures with no `action` render exactly as they do today — `FailureNotice` gains no
  routing knowledge of its own.
- The stale "no CTA" comment is corrected.
- `npm test` passes.
