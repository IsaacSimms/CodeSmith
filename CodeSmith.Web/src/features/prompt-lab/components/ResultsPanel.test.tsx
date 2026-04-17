// == Results Panel Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ResultsPanel } from "./ResultsPanel";
import type { AttemptResult } from "../types";

const defaultResult: AttemptResult = {
  attemptId:       "attempt-1",
  totalScore:      4,
  maxScore:        5,
  overallFeedback: "Good effort. Near-perfect JSON output.",
  submittedAt:     "2026-04-16T00:00:00Z",
  results: [
    {
      inputId:          "input-1",
      label:            "Solar planets",
      simulationOutput: '["Mercury","Venus","Earth"]',
      passed:           true,
      criterionScores:  [
        { criterionId: "valid-json",   criterionName: "Valid JSON",  points: 3, maxPoints: 3 },
        { criterionId: "no-preamble",  criterionName: "No Preamble", points: 1, maxPoints: 2 },
      ],
      feedback: "Valid JSON but slight preamble detected.",
    },
  ],
};

describe("ResultsPanel", () => {
  it("shows idle message when there is no result and not evaluating", () => {
    render(<ResultsPanel result={null} isEvaluating={false} onClear={vi.fn()} />);

    expect(screen.getByText(/Submit a prompt/)).toBeInTheDocument();
  });

  it("shows evaluating indicator when isEvaluating is true", () => {
    render(<ResultsPanel result={null} isEvaluating={true} onClear={vi.fn()} />);

    expect(screen.getByText("Evaluating…")).toBeInTheDocument();
  });

  it("shows score badge when result is present", () => {
    render(<ResultsPanel result={defaultResult} isEvaluating={false} onClear={vi.fn()} />);

    // "4/5 pts" may appear in both the header badge and per-input score row
    const badges = screen.getAllByText("4/5 pts");
    expect(badges.length).toBeGreaterThanOrEqual(1);
  });

  it("shows pass indicator for passing test inputs", () => {
    render(<ResultsPanel result={defaultResult} isEvaluating={false} onClear={vi.fn()} />);

    expect(screen.getByText("✓")).toBeInTheDocument();
    expect(screen.getByText("Solar planets")).toBeInTheDocument();
  });

  it("shows fail indicator for failing test inputs", () => {
    const failResult: AttemptResult = {
      ...defaultResult,
      results: [{ ...defaultResult.results[0], passed: false }],
    };

    render(<ResultsPanel result={failResult} isEvaluating={false} onClear={vi.fn()} />);

    expect(screen.getByText("✗")).toBeInTheDocument();
  });

  it("shows overall feedback", () => {
    render(<ResultsPanel result={defaultResult} isEvaluating={false} onClear={vi.fn()} />);

    expect(screen.getByText("Good effort. Near-perfect JSON output.")).toBeInTheDocument();
  });

  it("calls onClear when clear button is clicked", async () => {
    const onClear = vi.fn();
    render(<ResultsPanel result={defaultResult} isEvaluating={false} onClear={onClear} />);

    await userEvent.click(screen.getByText("Clear"));
    expect(onClear).toHaveBeenCalledOnce();
  });

  it("score badge is green when score is perfect", () => {
    const perfectResult: AttemptResult = { ...defaultResult, totalScore: 5, maxScore: 5 };
    render(<ResultsPanel result={perfectResult} isEvaluating={false} onClear={vi.fn()} />);

    const badge = screen.getByText("5/5 pts");
    expect(badge.className).toContain("bg-green-900");
  });

  it("score badge is red when score is zero", () => {
    const zeroResult: AttemptResult = { ...defaultResult, totalScore: 0, maxScore: 5 };
    render(<ResultsPanel result={zeroResult} isEvaluating={false} onClear={vi.fn()} />);

    const badge = screen.getByText("0/5 pts");
    expect(badge.className).toContain("bg-red-900");
  });
});
