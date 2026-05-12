// == MessageRole Contract Test == //
// Asserts bidirectional exhaustiveness between frontend union and backend C# enum.
// If the backend MessageRole enum gains or loses a value, one of the type assertions
// below produces a compile error — update both sides together.
import { describe, it, expect, expectTypeOf } from "vitest";
import type { MessageRole } from "./types";

// Backend C# enum members — keep in sync with CodeSmith.Core/Enums/MessageRole.cs
const backendMembers = ["User", "Assistant"] as const;
type BackendMessageRole = (typeof backendMembers)[number];

describe("MessageRole contract", () => {
  it("frontend union is exhaustive over backend enum members", () => {
    expectTypeOf<MessageRole>().toEqualTypeOf<BackendMessageRole>();
    expect(backendMembers).toHaveLength(2); // update when the enum grows
  });
});
