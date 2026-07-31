// == Account label resolution == //
import { describe, it, expect } from "vitest";
import type { AccountInfo } from "@azure/msal-browser";
import { resolveAccountLabel } from "./resolveAccountLabel";

// Entra synthesizes {objectId}@{tenant}.onmicrosoft.com as the UPN for federated (social) accounts
const FEDERATED_UPN = "be36f73c-1993-4e1c-8064-8a4156082144@codesmithapp.onmicrosoft.com";

function account(overrides: Partial<AccountInfo> = {}): AccountInfo {
  return {
    homeAccountId: "home-account-id",
    environment: "codesmithapp.ciamlogin.com",
    tenantId: "25463a03-81a7-448c-9873-99d2ecc03eb8",
    localAccountId: "be36f73c-1993-4e1c-8064-8a4156082144",
    username: "",
    ...overrides,
  };
}

describe("resolveAccountLabel", () => {
  it("prefers the email claim", () => {
    const label = resolveAccountLabel(
      account({
        username: FEDERATED_UPN,
        name: "IsaacTestGoogleAuth",
        idTokenClaims: { email: "isaacsimms11@gmail.com" },
      })
    );

    expect(label).toBe("isaacsimms11@gmail.com");
  });

  it("falls back to an email-shaped username for local accounts", () => {
    const label = resolveAccountLabel(account({ username: "user@example.com" }));

    expect(label).toBe("user@example.com");
  });

  it("never renders the synthesized GUID UPN of a federated account", () => {
    const label = resolveAccountLabel(
      account({ username: FEDERATED_UPN, name: "IsaacTestGoogleAuth" })
    );

    expect(label).toBe("IsaacTestGoogleAuth");
    expect(label).not.toContain("be36f73c");
  });

  it("returns the fallback when a federated account has no other identity", () => {
    const label = resolveAccountLabel(account({ username: FEDERATED_UPN }));

    expect(label).toBe("Signed in");
  });

  it("falls back to the name claim when no usable email exists", () => {
    const label = resolveAccountLabel(account({ name: "IsaacTestGoogleAuth" }));

    expect(label).toBe("IsaacTestGoogleAuth");
  });

  it("rejects the 'unknown' display-name placeholder Entra assigns", () => {
    const label = resolveAccountLabel(account({ name: "unknown" }));

    expect(label).toBe("Signed in");
  });

  it("returns the fallback when username is the empty string MSAL emits", () => {
    const label = resolveAccountLabel(account({ username: "" }));

    expect(label).toBe("Signed in");
  });
});
