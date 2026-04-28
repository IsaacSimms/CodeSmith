// == Token Usage Bar Tests == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { TokenUsageBar } from "./TokenUsageBar";

const defaultProps = {
  tokensUsed: 1000,
  contextWindowSize: 200_000,
  label: "Context",
  description: "Conversation history sent to the AI this turn. Grows with each message.",
};

describe("TokenUsageBar", () => {
  it("renders without crashing", () => {
    const { container } = render(<TokenUsageBar {...defaultProps} />);
    expect(container.firstChild).not.toBeNull();
  });

  it("renders the label text in the tooltip", () => {
    render(<TokenUsageBar {...defaultProps} />);
    expect(screen.getByText("Context")).toBeInTheDocument();
  });

  it("renders the token count string in the tooltip", () => {
    render(<TokenUsageBar {...defaultProps} />);
    expect(screen.getByText(/1,000\s*\/\s*200,000 tokens/)).toBeInTheDocument();
  });

  it("renders the description in the tooltip", () => {
    render(<TokenUsageBar {...defaultProps} />);
    expect(screen.getByText(defaultProps.description)).toBeInTheDocument();
  });

  it("sets bar fill width proportional to tokens used", () => {
    render(
      <TokenUsageBar tokensUsed={100_000} contextWindowSize={200_000} label="Context" description="desc" />
    );
    // 50% — find the inner fill div by its inline style
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.style.width).toBe("50%");
  });

  it("clamps bar fill to 100% when tokens exceed the window", () => {
    render(
      <TokenUsageBar tokensUsed={300_000} contextWindowSize={200_000} label="Context" description="desc" />
    );
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.style.width).toBe("100%");
  });

  it("uses emerald color below 60% usage", () => {
    render(
      <TokenUsageBar tokensUsed={50_000} contextWindowSize={200_000} label="Context" description="desc" />
    );
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.className).toContain("bg-emerald-500");
  });

  it("uses yellow color at 60–79% usage", () => {
    render(
      <TokenUsageBar tokensUsed={140_000} contextWindowSize={200_000} label="Context" description="desc" />
    );
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.className).toContain("bg-yellow-400");
  });

  it("uses red color at 80%+ usage", () => {
    render(
      <TokenUsageBar tokensUsed={180_000} contextWindowSize={200_000} label="Context" description="desc" />
    );
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.className).toContain("bg-red-500");
  });

  it("applies a minimum visual fill so the bar is never invisible", () => {
    render(
      <TokenUsageBar tokensUsed={0} contextWindowSize={200_000} label="Context" description="desc" />
    );
    const fill = document.querySelector("[style]") as HTMLElement;
    // display width is Math.max(0, 0.3) = 0.3
    expect(fill.style.width).toBe("0.3%");
  });
});
