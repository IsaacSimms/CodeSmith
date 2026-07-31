// == Client Error Interpretation Tests == //
import { describe, it, expect } from "vitest";
import { ApiClientError } from "./apiClient";
import { interpretError } from "./clientError";

describe("interpretError", () => {
  it("maps 402 to paywall fixed copy", () => {
    const err = new ApiClientError(402, {
      title: "Insufficient quota or credits",
      detail: "Insufficient quota or credits for this request.",
      status: 402,
    });

    const failure = interpretError(err);

    expect(failure.kind).toBe("paywall");
    expect(failure.title).toBe("Out of free quota and credits");
    expect(failure.detail).toContain("credits");
  });

  it("maps 401 to login fixed copy", () => {
    const err = new ApiClientError(401, {
      title: "Login required",
      detail: "Sign in with an account to use tokens.",
      status: 401,
      code: "login_required",
    });

    expect(interpretError(err).kind).toBe("login");
    expect(interpretError(err).title).toBe("Sign in required");
  });

  it("maps 404 to notFound", () => {
    const err = new ApiClientError(404, { title: "Session not found", detail: "gone", status: 404 });
    expect(interpretError(err).kind).toBe("notFound");
  });

  it("maps 502 to ai", () => {
    const err = new ApiClientError(502, {
      title: "AI service error",
      detail: "Failed to get guidance. Please try again.",
      status: 502,
    });
    expect(interpretError(err).kind).toBe("ai");
    expect(interpretError(err).title).toBe("AI service error");
  });

  it("maps non-ApiClientError Error to generic with message", () => {
    const failure = interpretError(new Error("AI service error"));
    expect(failure.kind).toBe("generic");
    expect(failure.detail).toBe("AI service error");
  });

  it("maps unknown values to generic fixed copy", () => {
    const failure = interpretError("boom");
    expect(failure.kind).toBe("generic");
    expect(failure.title).toBe("Something went wrong");
  });
});
