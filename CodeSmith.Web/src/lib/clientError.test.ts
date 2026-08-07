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
    expect(failure.action).toEqual({ label: "Add credits", href: "/account#credits" });
  });

  it("maps 401 to login fixed copy", () => {
    const err = new ApiClientError(401, {
      title: "Login required",
      detail: "Sign in with an account to use tokens.",
      status: 401,
      code: "login_required",
    });

    const failure = interpretError(err);
    expect(failure.kind).toBe("login");
    expect(failure.title).toBe("Sign in required");
    expect(failure.action).toEqual({ label: "Sign in", href: "/account" });
  });

  it("maps 404 to notFound without action", () => {
    const err = new ApiClientError(404, { title: "Session not found", detail: "gone", status: 404 });
    const failure = interpretError(err);
    expect(failure.kind).toBe("notFound");
    expect(failure.action).toBeUndefined();
  });

  it("maps 502 to ai without action", () => {
    const err = new ApiClientError(502, {
      title: "AI service error",
      detail: "Failed to get guidance. Please try again.",
      status: 502,
    });
    const failure = interpretError(err);
    expect(failure.kind).toBe("ai");
    expect(failure.title).toBe("AI service error");
    expect(failure.action).toBeUndefined();
  });

  it("maps non-ApiClientError Error to generic with message and no action", () => {
    const failure = interpretError(new Error("AI service error"));
    expect(failure.kind).toBe("generic");
    expect(failure.detail).toBe("AI service error");
    expect(failure.action).toBeUndefined();
  });

  it("maps unknown values to generic fixed copy without action", () => {
    const failure = interpretError("boom");
    expect(failure.kind).toBe("generic");
    expect(failure.title).toBe("Something went wrong");
    expect(failure.action).toBeUndefined();
  });
});
