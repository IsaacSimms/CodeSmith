// == Prompt Lab Window Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { PromptLabWindow } from "./PromptLabWindow";
import * as apiClient from "../../../lib/apiClient";
import { NavigationProvider } from "../../../contexts/NavigationContext";
import type { ChallengeResponse, PromptLabSession, AttemptResult } from "../types";

vi.mock("../../../lib/apiClient");
vi.mock("../../../contexts/ProviderPreferenceContext", () => ({
  useProviderPreferenceContext: () => ({
    provider: "Xai",
    setProvider: vi.fn(),
    availableProviders: ["Anthropic", "OpenAi", "Xai"],
    isReady: true,
  }),
}));
vi.mock("@monaco-editor/react", () => ({ // Monaco doesn't run in jsdom
  default: ({ onChange, value }: { onChange?: (v: string) => void; value?: string }) => (
    <textarea
      data-testid="monaco-editor"
      value={value ?? ""}
      onChange={(e) => onChange?.(e.target.value)}
    />
  ),
}));

const mockChallenge: ChallengeResponse = {
  challengeId:        "format-json-01",
  title:              "JSON Only",
  description:        "Return valid JSON arrays with no preamble.",
  category:           "OutputFormatControl",
  difficulty:         "Medium",
  lockedSystemPrompt: "You are a helpful assistant.",
  editableFields:     [{ fieldType: "SystemPrompt", placeholder: "Add instructions…", defaultValue: "" }],
  testInputs:         [
    { inputId: "input-1", label: "Solar planets" },
    { inputId: "input-2", label: "Primary colors" },
    { inputId: "input-3", label: "Languages" },
  ],
  rubric: [
    { criterionId: "valid-json",  name: "Valid JSON",  description: "Output is valid JSON.", maxPoints: 3 },
    { criterionId: "no-preamble", name: "No Preamble", description: "No preamble.",           maxPoints: 2 },
  ],
};

const mockSession: PromptLabSession = {
  sessionId:   "session-abc",
  challengeId: "format-json-01",
  provider:    "Anthropic",
  testInputs:  mockChallenge.testInputs,
  attempts:    [],
  createdAt:   "2026-04-16T00:00:00Z",
  dynamicInputsGenerated: true,
};

const mockAttemptResult: AttemptResult = {
  attemptId:        "attempt-1",
  totalScore:       4,
  maxScore:         5,
  overallFeedback:  "Good attempt.",
  submittedAt:      "2026-04-16T00:01:00Z",
  promptTokensUsed: 500,
  contextWindowSize: 200_000,
  adversarialHint:  "Try harder.",
  results: [
    {
      inputId: "input-1", label: "Solar planets", userMessage: "List solar planets in JSON format",
      simulationOutput: '["Mercury"]', passed: true,
      criterionScores: [{ criterionId: "valid-json", criterionName: "Valid JSON", points: 3, maxPoints: 3 }],
      feedback: "Correct JSON.",
    },
  ],
};

function renderPromptLabWindow() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NavigationProvider>
          <PromptLabWindow />
        </NavigationProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe("PromptLabWindow", () => {
  describe("before a challenge is selected", () => {
    it("renders the challenge selector with Prompt Lab heading", async () => {
      vi.mocked(apiClient.getChallenges).mockResolvedValue([mockChallenge]);

      renderPromptLabWindow();

      await waitFor(() => {
        expect(screen.getByText("Prompt Lab")).toBeInTheDocument();
      });
    });

    it("shows challenge titles from the catalog", async () => {
      vi.mocked(apiClient.getChallenges).mockResolvedValue([mockChallenge]);

      renderPromptLabWindow();

      await waitFor(() => {
        expect(screen.getByText("JSON Only")).toBeInTheDocument();
      });
    });

    it("shows loading state while challenges are being fetched", () => {
      vi.mocked(apiClient.getChallenges).mockReturnValue(new Promise(() => {})); // Never resolves

      renderPromptLabWindow();

      expect(screen.getByText(/Loading challenges/)).toBeInTheDocument();
    });
  });

  describe("after selecting a challenge", () => {
    async function renderWithSession() {
      vi.mocked(apiClient.getChallenges).mockResolvedValue([mockChallenge]);
      vi.mocked(apiClient.startPromptLabChallenge).mockResolvedValue(mockSession);

      renderPromptLabWindow();

      await waitFor(() => screen.getByText("JSON Only"));
      await userEvent.click(screen.getByText("JSON Only"));

      await waitFor(() => {
        expect(screen.getByText("Submit Prompt")).toBeInTheDocument();
      });
    }

    it("sends the selected provider from context, not a hardcoded Anthropic default", async () => {
      vi.mocked(apiClient.getChallenges).mockResolvedValue([mockChallenge]);
      vi.mocked(apiClient.startPromptLabChallenge).mockResolvedValue(mockSession);

      renderPromptLabWindow();
      await waitFor(() => screen.getByText("JSON Only"));
      await userEvent.click(screen.getByText("JSON Only"));

      await waitFor(() => {
        expect(vi.mocked(apiClient.startPromptLabChallenge).mock.calls[0]?.[0]).toEqual({
          challengeId: "format-json-01",
          provider: "Xai",
        });
      });
    });

    it("shows the challenge panel with Submit Prompt button", async () => {
      await renderWithSession();

      expect(screen.getByText("Submit Prompt")).toBeInTheDocument();
    });

    it("shows the challenge description", async () => {
      await renderWithSession();

      expect(screen.getByText(/Return valid JSON arrays/)).toBeInTheDocument();
    });

    it("shows test input labels", async () => {
      await renderWithSession();

      expect(screen.getByText("Solar planets")).toBeInTheDocument();
    });

    it("submits attempt and shows results on success", async () => {
      vi.mocked(apiClient.getChallenges).mockResolvedValue([mockChallenge]);
      vi.mocked(apiClient.startPromptLabChallenge).mockResolvedValue(mockSession);
      vi.mocked(apiClient.submitPromptLabAttempt).mockResolvedValue(mockAttemptResult);

      renderPromptLabWindow();

      await waitFor(() => screen.getByText("JSON Only"));
      await userEvent.click(screen.getByText("JSON Only"));
      await waitFor(() => screen.getByText("Submit Prompt"));
      await userEvent.click(screen.getByText("Submit Prompt"));

      await waitFor(() => {
        // "4/5 pts" appears in both the header badge and per-input score row
        expect(screen.getAllByText("4/5 pts").length).toBeGreaterThanOrEqual(1);
      });
    });

    it("shows FailureNotice near Submit when evaluation returns 402", async () => {
      // Duck-typed ApiClientError — full vi.mock of apiClient replaces the real class
      const quotaError = Object.assign(new Error("Out of credits."), {
        name: "ApiClientError",
        statusCode: 402,
        apiError: {
          title: "Insufficient quota or credits",
          detail: "Out of credits.",
          status: 402,
        },
      });
      vi.mocked(apiClient.getChallenges).mockResolvedValue([mockChallenge]);
      vi.mocked(apiClient.startPromptLabChallenge).mockResolvedValue(mockSession);
      vi.mocked(apiClient.submitPromptLabAttempt).mockRejectedValue(quotaError);

      renderPromptLabWindow();

      await waitFor(() => screen.getByText("JSON Only"));
      await userEvent.click(screen.getByText("JSON Only"));
      await waitFor(() => screen.getByText("Submit Prompt"));
      await userEvent.click(screen.getByText("Submit Prompt"));

      await waitFor(() => {
        expect(screen.getAllByTestId("failure-notice").length).toBeGreaterThanOrEqual(1);
      });
      expect(screen.getAllByText("Out of free quota and credits").length).toBeGreaterThanOrEqual(1);
    });
  });
});
