// == Challenge Selector Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChallengeSelector } from "./ChallengeSelector";
import type { ChallengeResponse } from "../types";

const mockChallenges: ChallengeResponse[] = [
  {
    challengeId:       "format-json-01",
    title:             "JSON Only",
    description:       "Return valid JSON arrays with no preamble.",
    category:          "OutputFormatControl",
    difficulty:        "Medium",
    lockedSystemPrompt: "You are a helpful assistant.",
    editableFields:    [{ fieldType: "SystemPrompt", placeholder: "Add instructions…", defaultValue: "" }],
    testInputs:        [
      { inputId: "input-1", label: "Solar planets" },
      { inputId: "input-2", label: "Primary colors" },
      { inputId: "input-3", label: "Languages" },
    ],
    rubric: [
      { criterionId: "valid-json", name: "Valid JSON", description: "Output is valid JSON.", maxPoints: 3 },
      { criterionId: "no-preamble", name: "No Preamble", description: "No conversational preamble.", maxPoints: 2 },
    ],
  },
  {
    challengeId:       "scope-capitals-01",
    title:             "Capital Cities Only",
    description:       "Return only the capital city name.",
    category:          "SpecificityOfScope",
    difficulty:        "Easy",
    lockedSystemPrompt: "You are a geography assistant.",
    editableFields:    [{ fieldType: "UserMessage", placeholder: "Ask for just the capital…", defaultValue: "" }],
    testInputs:        [
      { inputId: "input-1", label: "France" },
      { inputId: "input-2", label: "Australia" },
      { inputId: "input-3", label: "Kazakhstan" },
    ],
    rubric: [
      { criterionId: "correct-capital", name: "Correct Capital", description: "Correct city.", maxPoints: 3 },
    ],
  },
];

describe("ChallengeSelector", () => {
  it("renders challenge titles", () => {
    render(
      <ChallengeSelector
        challenges={mockChallenges}
        isLoading={false}
        isStarting={false}
        onSelect={vi.fn()}
      />
    );

    expect(screen.getByText("JSON Only")).toBeInTheDocument();
    expect(screen.getByText("Capital Cities Only")).toBeInTheDocument();
  });

  it("groups challenges by category", () => {
    render(
      <ChallengeSelector
        challenges={mockChallenges}
        isLoading={false}
        isStarting={false}
        onSelect={vi.fn()}
      />
    );

    expect(screen.getByText("Output Format Control")).toBeInTheDocument();
    expect(screen.getByText("Specificity of Scope")).toBeInTheDocument();
  });

  it("shows difficulty badges", () => {
    render(
      <ChallengeSelector
        challenges={mockChallenges}
        isLoading={false}
        isStarting={false}
        onSelect={vi.fn()}
      />
    );

    expect(screen.getByText("Medium")).toBeInTheDocument();
    expect(screen.getByText("Easy")).toBeInTheDocument();
  });

  it("calls onSelect with challengeId when a challenge is clicked", async () => {
    const onSelect = vi.fn();
    render(
      <ChallengeSelector
        challenges={mockChallenges}
        isLoading={false}
        isStarting={false}
        onSelect={onSelect}
      />
    );

    await userEvent.click(screen.getByText("JSON Only"));
    expect(onSelect).toHaveBeenCalledOnce();
    expect(onSelect).toHaveBeenCalledWith("format-json-01");
  });

  it("shows loading state when isLoading is true", () => {
    render(
      <ChallengeSelector
        challenges={[]}
        isLoading={true}
        isStarting={false}
        onSelect={vi.fn()}
      />
    );

    expect(screen.getByText(/Loading challenges/)).toBeInTheDocument();
  });

  it("disables challenge buttons while isStarting is true", () => {
    render(
      <ChallengeSelector
        challenges={mockChallenges}
        isLoading={false}
        isStarting={true}
        onSelect={vi.fn()}
      />
    );

    const buttons = screen.getAllByRole("button");
    buttons.forEach((btn) => expect(btn).toBeDisabled());
  });

  it("shows test input count and rubric count", () => {
    const challenges = [mockChallenges[0]] as ChallengeResponse[];
    render(
      <ChallengeSelector
        challenges={challenges}
        isLoading={false}
        isStarting={false}
        onSelect={vi.fn()}
      />
    );

    expect(screen.getByText(/3 test inputs/)).toBeInTheDocument();
    expect(screen.getByText(/2 criteria/)).toBeInTheDocument();
  });
});
