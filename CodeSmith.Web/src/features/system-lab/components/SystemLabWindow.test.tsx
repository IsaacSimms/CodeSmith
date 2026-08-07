// == System Lab Window Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { SystemLabWindow } from "./SystemLabWindow";
import * as apiClient from "../../../lib/apiClient";
import { NavigationProvider } from "../../../contexts/NavigationContext";
import type { ScenarioResponse, SystemLabSession, AttemptResult } from "../types";

vi.mock("../../../lib/apiClient");
vi.mock("../../../contexts/ProviderPreferenceContext", () => ({
  useProviderPreferenceContext: () => ({
    provider: "Xai",
    setProvider: vi.fn(),
    availableProviders: ["Anthropic", "OpenAi", "Xai"],
    isReady: true,
  }),
}));

const mockScenario: ScenarioResponse = {
  scenarioId:        "storage-access-01",
  title:             "Storage Account Access for a Web App",
  description:       "A single-tenant web application needs read-only access to blob storage.",
  constraints:       "- The application must be able to read blobs from one specific container.",
  category:          "IdentityAndGovernance",
  difficulty:        "Easy",
  evaluationMode:    "SingleAnswer",
  requiredTradeoffs: ["Why is assigning Contributor at the subscription level dangerous?"],
  rubric: [
    { criterionId: "least-privilege", name: "Least Privilege", description: "Chooses a minimum-privilege role.", maxPoints: 4 },
    { criterionId: "scope",           name: "Scope",           description: "Justifies the assignment scope.",   maxPoints: 2 },
  ],
};

const mockSession: SystemLabSession = {
  sessionId:  "session-abc",
  scenarioId: "storage-access-01",
  attempts:   [],
  createdAt:  "2026-07-30T00:00:00Z",
};

const mockAttemptResult: AttemptResult = {
  attemptId:           "attempt-1",
  rubricScore:         5,
  maxRubricScore:      6,
  dimensionDeductions: [],
  totalScore:          5,
  maxScore:            6,
  overallFeedback:     "Solid reasoning.",
  criterionScores:     [{ criterionId: "least-privilege", criterionName: "Least Privilege", points: 4, maxPoints: 4 }],
  tradeoffResults:     [],
  submittedAt:         "2026-07-30T00:01:00Z",
};

function renderSystemLabWindow() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <NavigationProvider>
          <SystemLabWindow />
        </NavigationProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
});

describe("SystemLabWindow", () => {
  describe("before a scenario is selected", () => {
    it("renders the scenario selector with System Lab heading", async () => {
      vi.mocked(apiClient.getScenarios).mockResolvedValue([mockScenario]);

      renderSystemLabWindow();

      await waitFor(() => {
        expect(screen.getByText("System Lab")).toBeInTheDocument();
      });
    });

    it("shows loading state while scenarios are being fetched", () => {
      vi.mocked(apiClient.getScenarios).mockReturnValue(new Promise(() => {})); // Never resolves

      renderSystemLabWindow();

      expect(screen.getByText(/Loading scenarios/)).toBeInTheDocument();
    });
  });

  describe("after selecting a scenario", () => {
    async function renderWithSession() {
      vi.mocked(apiClient.getScenarios).mockResolvedValue([mockScenario]);
      vi.mocked(apiClient.startSystemLabSession).mockResolvedValue(mockSession);

      renderSystemLabWindow();

      await waitFor(() => screen.getByText(mockScenario.title));
      await userEvent.click(screen.getByText(mockScenario.title));

      await waitFor(() => {
        expect(screen.getByRole("button", { name: "Submit Justification" })).toBeInTheDocument();
      });
    }

    it("sends the selected provider from context, not a hardcoded Anthropic default", async () => {
      vi.mocked(apiClient.getScenarios).mockResolvedValue([mockScenario]);
      vi.mocked(apiClient.startSystemLabSession).mockResolvedValue(mockSession);

      renderSystemLabWindow();
      await waitFor(() => screen.getByText(mockScenario.title));
      await userEvent.click(screen.getByText(mockScenario.title));

      await waitFor(() => {
        expect(vi.mocked(apiClient.startSystemLabSession).mock.calls[0]?.[0]).toEqual({
          scenarioId: "storage-access-01",
          provider: "Xai",
        });
      });
    });

    // The submit control mirrors Prompt Lab's: a full-width button anchored under the
    // scenario info in the right panel, not a compact button in the session badge row.
    it("renders a full-width submit button in the right panel", async () => {
      await renderWithSession();

      expect(screen.getByRole("button", { name: "Submit Justification" })).toHaveClass("w-full");
    });

    it("keeps the session badge row free of buttons", async () => {
      await renderWithSession();

      const badgeRow = screen.getByText(mockScenario.difficulty).parentElement;
      expect(badgeRow?.querySelector("button")).toBeNull();
    });

    it("shows the keyboard hint once, beneath the submit button", async () => {
      await renderWithSession();

      expect(screen.getAllByText(/to submit/)).toHaveLength(1);
    });

    it("disables submit until the justification has content", async () => {
      await renderWithSession();

      expect(screen.getByRole("button", { name: "Submit Justification" })).toBeDisabled();

      await userEvent.type(
        screen.getByPlaceholderText(/Write your infrastructure design justification/),
        "Use a scoped RBAC role."
      );

      expect(screen.getByRole("button", { name: "Submit Justification" })).toBeEnabled();
    });

    it("submits the attempt and shows evaluation results", async () => {
      vi.mocked(apiClient.submitSystemLabAttempt).mockResolvedValue(mockAttemptResult);

      await renderWithSession();

      await userEvent.type(
        screen.getByPlaceholderText(/Write your infrastructure design justification/),
        "Use Storage Blob Data Reader scoped to the container."
      );
      await userEvent.click(screen.getByRole("button", { name: "Submit Justification" }));

      await waitFor(() => {
        expect(screen.getByText("5/6 pts")).toBeInTheDocument();
      });
    });

    it("shows the next attempt number beneath the submit button after an attempt", async () => {
      vi.mocked(apiClient.submitSystemLabAttempt).mockResolvedValue(mockAttemptResult);

      await renderWithSession();

      await userEvent.type(
        screen.getByPlaceholderText(/Write your infrastructure design justification/),
        "Use Storage Blob Data Reader scoped to the container."
      );
      await userEvent.click(screen.getByRole("button", { name: "Submit Justification" }));

      await waitFor(() => {
        expect(screen.getByText("Attempt 2")).toBeInTheDocument();
      });
    });
  });
});
