// == MSAL login request helpers == //
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { buildGoogleLoginRequest, buildLoginRequest } from "./msalConfig";

beforeEach(() => {
  vi.unstubAllEnvs();
  vi.stubEnv("VITE_AAD_API_SCOPE", "api://test-client/access");
});

afterEach(() => {
  vi.unstubAllEnvs();
});

describe("buildLoginRequest", () => {
  it("returns API scope only (email / local CIAM path)", () => {
    expect(buildLoginRequest()).toEqual({
      scopes: ["api://test-client/access"],
    });
  });

  it("does not set domain_hint", () => {
    const req = buildLoginRequest();
    expect(req).not.toHaveProperty("extraQueryParameters");
  });
});

describe("buildGoogleLoginRequest", () => {
  it("includes API scope and domain_hint google", () => {
    expect(buildGoogleLoginRequest()).toEqual({
      scopes: ["api://test-client/access"],
      extraQueryParameters: { domain_hint: "google" },
    });
  });
});
