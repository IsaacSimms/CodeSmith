# User-Based (Manual) Testing Guide

This guide covers **manual, end-user testing** of the entire CodeSmith application as a real user would experience it in the browser. It focuses on the three main product modes, cross-cutting features (auth, quota, code execution, AI providers), and exploratory testing.

It complements (but does not replace) the automated test suites (`dotnet test`, Vitest, Playwright E2E).

## Prerequisites

- Full local development environment set up (see [README.md#development-how-to](README.md)).
- Docker Desktop running (for Piston sandbox).
- Valid API keys configured in `CodeSmith.Api/appsettings.Development.json` (Anthropic recommended; others optional).
- For **full quota / usage enforcement testing** (recommended for complete coverage):
  - A SQL Server instance with a connection string.
  - Add to `CodeSmith.Api/appsettings.Development.json` (or user secrets):

    ```json
    "ConnectionStrings": {
      "CodeSmithDb": "Server=(localdb)\\MSSQLLocalDB;Database=CodeSmithDev;Trusted_Connection=True;TrustServerCertificate=True;"
    }
    ```

    > LocalDB is convenient on Windows. Alternatives: Docker SQL Server, Azure SQL, or any reachable SQL Server.

- Browser that can accept the self-signed dev cert at `https://localhost:5173`.

## Starting the Full Stack

1. Start Piston (code execution sandbox):

   ```powershell
   docker compose up -d piston
   ```

2. Start the Backend API (in one terminal):

   ```powershell
   dotnet run --project CodeSmith.Api --launch-profile https
   ```

   - Listens on `https://localhost:7111` and `http://localhost:5175`.
   - Swagger: `https://localhost:7111/swagger`.

3. Start the Frontend (in another terminal):

   ```powershell
   cd CodeSmith.Web
   npm run dev
   ```

   - Runs at `https://localhost:5173`.
   - Proxies API calls to the backend.
   - Accept the browser security warning for the dev cert on first load.

Optional: Start the CLI for quick backend-only testing:

```powershell
dotnet run --project CodeSmith.CLI
```

## Authenticating as a Test User (Dev Mode)

The app uses minimal auth. For manual testing you "log in" by sending a header:

- Header: `X-Debug-User-Id: your-test-user-123`

You can set this in:
- Browser DevTools → Network → Edit and Resend (or use a extension like ModHeader).
- curl / Postman / REST clients.
- For browser flows, the frontend does **not** send it automatically — you must use a proxy/extension or temporarily patch the frontend for testing.

Different header values act as different "users" for quota, history, etc.

**Important for quota testing:** The new usage system ties free quota to the `objectId` (from this header or real Entra claims) plus IP-based caps.

## Main User Journeys

### 1. Tutoring / Paired Programmer (Core Flow)

1. On the home page, select a language and difficulty.
2. Click to create a new problem.
   - Backend generates a problem description + starter code using the Accurate model.
3. Interact in the split-screen editor:
   - Edit the starter code.
   - Use the chat to ask for guidance (Socratic, no direct answers).
   - The AI always sees the current editor contents.
4. Click **Test Code** (or Run).
   - Code executes in the sandbox (Piston by default).
   - Output appears; the AI can be asked to interpret results.
5. Iterate: fix code based on guidance + run results.
6. Try multiple difficulties/languages.

**Things to test manually:**
- Problem generation quality and variety.
- Guidance helpfulness vs. spoiling the answer.
- Editor + run loop feels responsive.
- Long conversations (history handling).
- Code with errors, infinite loops (sandbox limits).
- Switching languages mid-session.

### 2. Prompt Lab

1. Navigate to Prompt Lab.
2. Browse challenges (different categories/difficulties).
3. Pick one and start a session.
4. Edit the **System Prompt** (your additions) and **User Message**.
5. Click **Submit**.
   - Runs your prompt against multiple hidden test inputs.
   - Includes a locked base prompt + hidden adversarial instruction.
   - Two phases: simulation (Fast model) + evaluation (model depends on quota).
6. Review results: per-test pass/fail + rubric scores + AI feedback.
7. Use the guidance chat to iterate on your prompt.
8. Try "gaming" the system (see how well the adversarial is handled).

**Key things to observe:**
- How well your prompt resists the hidden adversarial.
- Quality of the AI evaluator's scores and explanations.
- Parallel execution speed.
- Ability to improve score over multiple attempts.

### 3. System Lab

1. Go to System Lab.
2. Browse scenarios (tradeoff reasoning, architecture decisions).
3. Start a scenario.
4. Write a justification document in the editor.
5. Submit.
   - AI evaluates against rubric + cross-cutting dimensions.
6. Receive detailed scoring and feedback.
7. Use chat for guidance without spoiling the answer.
8. Try different evaluation modes if available.

**Focus areas:**
- Depth of architectural reasoning in feedback.
- Consistency of dimension deductions.
- Guidance quality (Socratic vs. direct).

## Testing Usage Quota & Enforcement

With the recent changes, quota is enforced before every LLM call that costs money.

**Test scenarios:**

- Start with a fresh `X-Debug-User-Id`.
- Perform a full flow and track approximate token usage.
- Exhaust the free 20k (or configured amount) — the grant is one-time, with no expiry and no reset.
- Verify that further expensive actions are blocked (402 response / UI message).
- Test the "lenient last action" behavior: when you are very close to the limit, the system should still let you complete the current action.
- Test IP caps: from the same machine/IP, create multiple different debug users — they should share the per-IP pool.
- Verify the grant does not age out: an old `CreditBalances` row with `FreeTokensUsed` below `FreeQuotaMax` still gets free coverage. To re-test a spent grant, reset `FreeTokensUsed` to 0 in the DB.
- Paid credits path: if you manually set `PaidCreditsBalance` in the DB, verify it is used after free is exhausted.
- Switch between free and paid behavior.

**Useful DB tables to inspect:**
- `CreditBalances`
- `IpFreeUsages`
- `UsageLedgerEntries`

See the dedicated dev testing notes for quota hardening for exact header and DB manipulation tips.

## Testing Code Execution

- Use the **Test Code** button in Tutoring.
- Try all supported languages (C#, C++, Go, Rust, Python, Java, TypeScript).
- Test normal success, compilation errors, runtime errors, timeouts, large output.
- Verify sandbox isolation (no network, limited resources).
- Test the dev fallback (`LocalProcess`) if needed, but prefer Piston.

## Testing AI Providers

If you have keys for multiple providers:

- There is usually a provider selector (or it can be passed in requests).
- Test the same action with Anthropic, OpenAI, and xAI.
- Observe differences in:
  - Response quality/speed/cost (visible in logs or ledger).
  - Model used (Accurate vs Fast tier).
- Quota enforcement applies regardless of provider.

## Error States & Edge Cases

Manually exercise:

- No internet / API key invalid → graceful error messages.
- Rate limiting (many rapid requests).
- Quota exhaustion (see above).
- Very long conversations or large editor content.
- Malformed inputs in Prompt/System Lab.
- Code that tries to escape the sandbox.
- Concurrent actions in Prompt Lab (multiple submits or chats).
- Session expiration / not found.
- Switching tabs or refreshing mid-flow.

## Using the CLI for Rapid Testing

The `CodeSmith.CLI` provides a console interface that talks directly to the API. Useful for:

- Quickly creating sessions without the browser.
- Scripting repetitive test flows.
- Testing backend behavior in isolation from the UI.

## Automated Tests as a Complement

While this guide focuses on manual/user-based testing:

- Run `dotnet test` for backend logic.
- `cd CodeSmith.Web && npm test` for frontend components.
- `cd CodeSmith.Web && npx playwright test` for critical E2E user flows (requires the full stack running).

Playwright tests are especially valuable for verifying end-to-end happy paths after manual exploration.

## Tips for Effective User-Based Testing

- Use a consistent set of test `X-Debug-User-Id` values so you can track quota per "user".
- Keep the browser DevTools Network tab open to see API calls, headers, and errors.
- Watch backend console logs for model choices, token estimates, and quota decisions.
- Test on both desktop and (if possible) mobile viewports.
- Try "adversarial" user behavior: trying to get the AI to give direct answers, submit empty prompts, etc.
- Document surprising behaviors or UX friction you discover.

---

This guide is meant to be living. Add new scenarios as features evolve or bugs are found during manual exploration.