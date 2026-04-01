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
    expect(screen.getByText("Select a difficulty to begin")).toBeInTheDocument();
  });

  it("calls onSelect with the chosen difficulty", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<DifficultySelector onSelect={onSelect} isLoading={false} />);

    await user.click(screen.getByRole("button", { name: "Hard" }));

    expect(onSelect).toHaveBeenCalledOnce();
    expect(onSelect).toHaveBeenCalledWith("Hard");
  });

  it("disables buttons when loading", () => {
    render(<DifficultySelector onSelect={vi.fn()} isLoading={true} />);

    expect(screen.getByRole("button", { name: "Easy" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Medium" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Hard" })).toBeDisabled();
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
