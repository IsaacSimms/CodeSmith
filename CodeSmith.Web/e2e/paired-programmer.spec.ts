import { test, expect } from "@playwright/test";

// == Paired Programmer End-to-End Smoke Test == //
// Exercises: session creation -> editor interaction -> code execution (Piston)
// -> chat round-trip with Anthropic. Requires backend (7111), Piston (2000),
// and ANTHROPIC_API_KEY configured on the API.
test("full paired-programming flow: create session, run code, chat", async ({ page }) => {
  test.setTimeout(240_000);

  page.on("console", (msg) => console.log(`[browser:${msg.type()}]`, msg.text()));
  page.on("pageerror", (err) => console.log("[browser:pageerror]", err.message));
  page.on("response", async (resp) => {
    const url = resp.url();
    if (url.includes("/api/")) {
      console.log(`[api] ${resp.status()} ${resp.request().method()} ${url}`);
    }
  });

  await page.goto("/pairedprogrammer");

  // == Select Python + Easy to start a session == //
  await page.getByRole("radio", { name: "Python" }).click();
  await page.getByRole("button", { name: "Easy" }).click();

  // Session creation hits Anthropic, so give it room.
  await expect(page.getByRole("button", { name: "Test Code" })).toBeVisible({ timeout: 60_000 });

  // == Replace editor contents with a known-good solution == //
  const solution = [
    "def count_vowels(text):",
    "    return sum(1 for c in text.lower() if c in 'aeiou')",
    "",
    "print(count_vowels('Hello World'))",
    "print(count_vowels('AEIOU'))",
    "",
  ].join("\n");

  // Monaco's model is the source of truth; setValue fires onDidChangeModelContent
  // which @monaco-editor/react forwards to React state.
  await page.waitForFunction(() => {
    const w = window as unknown as { monaco?: { editor: { getEditors: () => unknown[] } } };
    return !!w.monaco && w.monaco.editor.getEditors().length > 0;
  });
  await page.evaluate((code) => {
    const w = window as unknown as {
      monaco: { editor: { getEditors: () => { setValue: (v: string) => void }[] } };
    };
    w.monaco.editor.getEditors()[0].setValue(code);
  }, solution);

  // == Run the code via Piston == //
  await page.getByRole("button", { name: "Test Code" }).click();

  // Wait for run to finish — status badge flips from "Running…" to "Exit: N".
  await expect(page.getByText(/^Exit: \d+$/)).toBeVisible({ timeout: 90_000 });

  // Terminal stdout is rendered in a <pre>. Scope to it to avoid false matches in
  // the editor line-numbers or problem description.
  const stdout = page.locator("pre.text-gray-200");
  await expect(stdout).toContainText("3");
  await expect(stdout).toContainText("5");

  // == Run triggers an auto-analysis chat message; wait for it to appear == //
  await expect(async () => {
    const bodyText = await page.locator("body").innerText();
    expect(bodyText).toMatch(/I just tested my code/);
  }).toPass({ timeout: 10_000 });

  // Wait for an assistant reply by polling for a message after our analysis.
  await expect(async () => {
    const bodyText = await page.locator("body").innerText();
    const idx = bodyText.indexOf("I just tested my code");
    expect(idx).toBeGreaterThan(-1);
    const remainder = bodyText.slice(idx + 200);
    expect(remainder.length).toBeGreaterThan(50);
  }).toPass({ timeout: 60_000 });

  // == Send a manual chat message and verify round-trip == //
  const chatInput = page.getByPlaceholder(/ask for guidance/i);
  await chatInput.fill("Reply with exactly the word PONG.");
  await chatInput.press("Enter");

  await expect(page.getByText(/PONG/i)).toBeVisible({ timeout: 60_000 });
});
