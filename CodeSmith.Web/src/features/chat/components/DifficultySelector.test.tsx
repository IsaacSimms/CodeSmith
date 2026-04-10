// == Difficulty Selector Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DifficultySelector } from "./DifficultySelector";

describe("DifficultySelector", () => {
  it("renders all three difficulty buttons", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={false} />);

    expect(screen.getByRole("button", { name: "Easy" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Medium" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Hard" })).toBeInTheDocument();
  });

  it("renders the title and subtitle", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={false} />);

    expect(screen.getByText("CodeSmith")).toBeInTheDocument();
    expect(screen.getByText("Pick a language and difficulty to begin")).toBeInTheDocument();
  });

  it("renders all six language pills", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={false} />);

    expect(screen.getByRole("radio", { name: "C#" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "C++" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Go" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Rust" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Python" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Java" })).toBeInTheDocument();
  });

  it("defaults to C# when no initialLanguage is provided", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={false} />);

    expect(screen.getByRole("radio", { name: "C#" })).toHaveAttribute("aria-checked", "true");
  });

  it("respects initialLanguage prop", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={false} initialLanguage="Rust" />);

    expect(screen.getByRole("radio", { name: "Rust" })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByRole("radio", { name: "C#" })).toHaveAttribute("aria-checked", "false");
  });

  it("calls onSelect with the chosen difficulty and default language", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<DifficultySelector onSelect={onSelect} isLoading={false} />);

    await user.click(screen.getByRole("button", { name: "Hard" }));

    expect(onSelect).toHaveBeenCalledOnce();
    expect(onSelect).toHaveBeenCalledWith("Hard", "CSharp");
  });

  it("calls onSelect with selected language after clicking a pill", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<DifficultySelector onSelect={onSelect} isLoading={false} />);

    await user.click(screen.getByRole("radio", { name: "Python" }));
    await user.click(screen.getByRole("button", { name: "Medium" }));

    expect(onSelect).toHaveBeenCalledWith("Medium", "Python");
  });

  it("disables buttons when loading", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={true} />);

    expect(screen.getByRole("button", { name: "Easy" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Medium" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Hard" })).toBeDisabled();
    expect(screen.getByRole("radio", { name: "C#" })).toBeDisabled();
  });

  it("shows loading text when isLoading is true", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={true} />);

    expect(screen.getByText("Generating problem...")).toBeInTheDocument();
  });

  it("does not show loading text when isLoading is false", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={false} />);

    expect(screen.queryByText("Generating problem...")).not.toBeInTheDocument();
  });
});
