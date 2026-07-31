// == Streaming Chat Tail Tests == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { StreamingChatTail } from "./StreamingChatTail";
import type { ClientFailure } from "../../../lib/clientError";

const paywall: ClientFailure = {
  kind: "paywall",
  title: "Out of free quota and credits",
  detail: "You don't have enough remaining free usage or purchased credits for this request.",
};

describe("StreamingChatTail", () => {
  it("shows FailureNotice without incomplete framing when there is no partial", () => {
    render(
      <StreamingChatTail
        isStreaming={false}
        streamingText=""
        failedTurn={{ failure: paywall }}
      />
    );

    expect(screen.getByTestId("failure-notice")).toBeInTheDocument();
    expect(screen.getByText("Out of free quota and credits")).toBeInTheDocument();
    expect(screen.queryByText(/incomplete and was not saved/i)).not.toBeInTheDocument();
  });

  it("shows partial bubble and incomplete framing when partial text exists", () => {
    render(
      <StreamingChatTail
        isStreaming={false}
        streamingText=""
        failedTurn={{ failure: paywall, partial: "half a hint" }}
      />
    );

    expect(screen.getByText("half a hint")).toBeInTheDocument();
    expect(screen.getByText(/incomplete and was not saved/i)).toBeInTheDocument();
  });
});
