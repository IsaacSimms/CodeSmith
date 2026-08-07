// == Difficulty Selector Tests == //
import { useState } from "react";
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { DifficultySelector } from "./DifficultySelector";
import type { Difficulty, Language, ProblemFocus, ProblemTopic } from "../types";

interface HarnessProps {
  onSelect?: (difficulty: Difficulty, language: Language) => void;
  isLoading?: boolean;
  initialLanguage?: Language;
  initialFocus?: ProblemFocus;
  initialTopic?: ProblemTopic;
}

// Focus and Topic are controlled by ChatWindow in production; this harness supplies that ownership
// so the selector can be exercised in isolation.
function renderSelector({
  onSelect = vi.fn(),
  isLoading = false,
  initialLanguage,
  initialFocus = "Random",
  initialTopic = "Random",
}: HarnessProps = {}) {
  function Harness() {
    const [focus, setFocus] = useState<ProblemFocus>(initialFocus);
    const [topic, setTopic] = useState<ProblemTopic>(initialTopic);

    return (
      <DifficultySelector
        onSelect={onSelect}
        isLoading={isLoading}
        initialLanguage={initialLanguage}
        focus={focus}
        topic={topic}
        onFocusChange={setFocus}
        onTopicChange={setTopic}
      />
    );
  }

  return render(<Harness />);
}

describe("DifficultySelector", () => {
  it("renders all three difficulty buttons", () => {
    renderSelector();

    expect(screen.getByRole("button", { name: "Easy" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Medium" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Hard" })).toBeInTheDocument();
  });

  it("renders a labeled Starting up… control while provider preference is not ready", () => {
    function Harness() {
      const [focus, setFocus] = useState<ProblemFocus>("Random");
      const [topic, setTopic] = useState<ProblemTopic>("Random");
      return (
        <DifficultySelector
          onSelect={vi.fn()}
          isLoading={false}
          isReady={false}
          focus={focus}
          topic={topic}
          onFocusChange={setFocus}
          onTopicChange={setTopic}
        />
      );
    }
    render(<Harness />);

    expect(screen.getByRole("button", { name: "Starting up…" })).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Easy" })).not.toBeInTheDocument();
  });

  it("renders the title and subtitle", () => {
    renderSelector();

    expect(screen.getByText("CodeSmith")).toBeInTheDocument();
    expect(screen.getByText("Pick a language and difficulty to begin")).toBeInTheDocument();
  });

  it("renders all seven language pills", () => {
    renderSelector();

    expect(screen.getByRole("radio", { name: "C#" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "C++" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Go" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Rust" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Python" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Java" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "TypeScript" })).toBeInTheDocument();
  });

  it("defaults to C# when no initialLanguage is provided", () => {
    renderSelector();

    expect(screen.getByRole("radio", { name: "C#" })).toHaveAttribute("aria-checked", "true");
  });

  it("respects initialLanguage prop", () => {
    renderSelector({ initialLanguage: "Rust" });

    expect(screen.getByRole("radio", { name: "Rust" })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByRole("radio", { name: "C#" })).toHaveAttribute("aria-checked", "false");
  });

  it("calls onSelect with the chosen difficulty, default language, and active provider", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    renderSelector({ onSelect });

    await user.click(screen.getByRole("button", { name: "Hard" }));

    expect(onSelect).toHaveBeenCalledOnce();
    expect(onSelect).toHaveBeenCalledWith("Hard", "CSharp");
  });

  it("calls onSelect with selected language after clicking a pill", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    renderSelector({ onSelect });

    await user.click(screen.getByRole("radio", { name: "Python" }));
    await user.click(screen.getByRole("button", { name: "Medium" }));

    expect(onSelect).toHaveBeenCalledWith("Medium", "Python");
  });

  it("disables buttons when loading", () => {
    renderSelector({ isLoading: true });

    expect(screen.getByRole("button", { name: "Easy" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Medium" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Hard" })).toBeDisabled();
    expect(screen.getByRole("radio", { name: "C#" })).toBeDisabled();
    expect(screen.getByLabelText("Focus")).toBeDisabled();
  });

  it("shows loading text when isLoading is true", () => {
    renderSelector({ isLoading: true });

    expect(screen.getByText("Generating problem...")).toBeInTheDocument();
  });

  it("does not show loading text when isLoading is false", () => {
    renderSelector();

    expect(screen.queryByText("Generating problem...")).not.toBeInTheDocument();
  });

  // == Focus and Topic Controls == //

  it("renders focus and topic selects defaulting to Random", () => {
    renderSelector();

    expect(screen.getByLabelText("Focus")).toHaveValue("Random");
    expect(screen.getByLabelText("Topic")).toHaveValue("Random");
  });

  it("offers every focus and topic option", () => {
    renderSelector();

    expect(screen.getByRole("option", { name: "Refactoring" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Standard implementation" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Real-world scenario" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Simulation & modeling" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Bit manipulation" })).toBeInTheDocument();
  });

  it("keeps the advanced disclosure collapsed by default", () => {
    renderSelector();

    expect(screen.getByTestId("advanced-summary").closest("details")).not.toHaveAttribute("open");
  });

  it("shows the variety helper text in the advanced disclosure", () => {
    renderSelector();

    expect(
      screen.getByText(/Pinning a topic reduces problem variety across regenerations/)
    ).toBeInTheDocument();
  });

  it("shows the current topic in the collapsed advanced summary and updates when it changes", async () => {
    // The summary is the only reminder that a topic is still pinned once the disclosure is closed
    const user = userEvent.setup();
    renderSelector();

    expect(screen.getByTestId("advanced-summary")).toHaveTextContent("Advanced — Topic: Random");

    await user.selectOptions(screen.getByLabelText("Topic"), "StateMachines");

    expect(screen.getByTestId("advanced-summary")).toHaveTextContent("Advanced — Topic: State machines");
  });

  it("reports focus and topic changes to the parent", async () => {
    const user = userEvent.setup();
    renderSelector();

    await user.selectOptions(screen.getByLabelText("Focus"), "BugFix");
    await user.selectOptions(screen.getByLabelText("Topic"), "DynamicProgramming");

    expect(screen.getByLabelText("Focus")).toHaveValue("BugFix");
    expect(screen.getByLabelText("Topic")).toHaveValue("DynamicProgramming");
  });

  it("respects an initial focus and topic selection", () => {
    renderSelector({ initialFocus: "Refactoring", initialTopic: "BitManipulation" });

    expect(screen.getByLabelText("Focus")).toHaveValue("Refactoring");
    expect(screen.getByTestId("advanced-summary")).toHaveTextContent("Topic: Bit manipulation");
  });
});
