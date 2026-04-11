// == Terminal Panel Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TerminalPanel } from "./TerminalPanel";

describe("TerminalPanel", () => {
  const defaultResult = { stdout: "hello\n", stderr: "", exitCode: 0, timedOut: false };

  it("renders stdout content", () => {
    render(<TerminalPanel result={defaultResult} isRunning={false} onClear={vi.fn()} />);

    expect(screen.getByText(/hello/)).toBeInTheDocument();
  });

  it("renders stderr in red styling", () => {
    const result = { ...defaultResult, stdout: "", stderr: "error occurred" };
    render(<TerminalPanel result={result} isRunning={false} onClear={vi.fn()} />);

    const stderr = screen.getByText("error occurred");
    expect(stderr.className).toContain("text-red-400");
  });

  it("shows green exit code badge for exit code 0", () => {
    render(<TerminalPanel result={defaultResult} isRunning={false} onClear={vi.fn()} />);

    const badge = screen.getByText("Exit: 0");
    expect(badge.className).toContain("bg-green-900");
  });

  it("shows red exit code badge for non-zero exit code", () => {
    const result = { ...defaultResult, exitCode: 1 };
    render(<TerminalPanel result={result} isRunning={false} onClear={vi.fn()} />);

    const badge = screen.getByText("Exit: 1");
    expect(badge.className).toContain("bg-red-900");
  });

  it("shows timed out badge when timedOut is true", () => {
    const result = { ...defaultResult, timedOut: true, exitCode: -1 };
    render(<TerminalPanel result={result} isRunning={false} onClear={vi.fn()} />);

    expect(screen.getByText("Timed out (10s)")).toBeInTheDocument();
  });

  it("does not show timed out badge when timedOut is false", () => {
    render(<TerminalPanel result={defaultResult} isRunning={false} onClear={vi.fn()} />);

    expect(screen.queryByText("Timed out (10s)")).not.toBeInTheDocument();
  });

  it("shows running indicator when isRunning is true and no result", () => {
    render(<TerminalPanel result={null} isRunning={true} onClear={vi.fn()} />);

    expect(screen.getByText("Running code…")).toBeInTheDocument();
  });

  it("shows running badge in header when isRunning is true", () => {
    render(<TerminalPanel result={null} isRunning={true} onClear={vi.fn()} />);

    expect(screen.getByText("Running…")).toBeInTheDocument();
  });

  it("shows (no output) when stdout and stderr are both empty", () => {
    const result = { ...defaultResult, stdout: "", stderr: "" };
    render(<TerminalPanel result={result} isRunning={false} onClear={vi.fn()} />);

    expect(screen.getByText("(no output)")).toBeInTheDocument();
  });

  it("calls onClear when clear button is clicked", async () => {
    const onClear = vi.fn();
    render(<TerminalPanel result={defaultResult} isRunning={false} onClear={onClear} />);

    await userEvent.click(screen.getByText("Clear"));
    expect(onClear).toHaveBeenCalledOnce();
  });
});
