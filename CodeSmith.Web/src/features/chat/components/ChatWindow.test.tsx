// == Chat Window Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ChatWindow } from "./ChatWindow";
import * as apiClient from "../../../lib/apiClient";
import { NavigationProvider } from "../../../contexts/NavigationContext";
import type { ChatResponse, ProblemSession } from "../types";

vi.mock("../../../lib/apiClient");
vi.mock("../../../hooks/useProviderPreference", () => ({
  useProviderPreference: () => ({ provider: "Anthropic", setProvider: vi.fn() }),
}));

const mockSession: ProblemSession = {
  sessionId: "test-session-id",
  difficulty: "Easy",
  language: "CSharp",
  focus: "Refactoring",       // Resolved server-side — a session never carries "Random"
  topic: "StateMachines",
  problemDescription: "Write a function that adds two numbers.",
  starterCode: "public int Add(int a, int b) {}",
  messages: [],
  createdAt: "2026-03-31T00:00:00Z",
};

const mockChatResponse: ChatResponse = { response: "Try a for loop", contextTokensUsed: 100, contextWindowSize: 200_000 };

function renderChatWindow(route = "/") {
  const queryClient = new QueryClient({
    defaultOptions: { mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <NavigationProvider>
          <ChatWindow />
        </NavigationProvider>
      </MemoryRouter>
    </QueryClientProvider>
  );
}

// The body of the Nth streamCreateSession call
function requestBody(callIndex = 0) {
  return vi.mocked(apiClient.streamCreateSession).mock.calls[callIndex]?.[0];
}

beforeEach(() => {
  vi.restoreAllMocks();
  vi.clearAllMocks();   // restoreAllMocks leaves mock.calls intact; call-index asserts need a clean slate
});

describe("ChatWindow", () => {
  describe("before session is created", () => {
    it("renders the difficulty selector", () => {
      renderChatWindow();

      expect(screen.getByText("CodeSmith")).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Easy" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Medium" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Hard" })).toBeInTheDocument();
    });

    it("does not render the chat input", () => {
      renderChatWindow();

      expect(screen.queryByPlaceholderText("Ask for guidance...")).not.toBeInTheDocument();
    });
  });

  describe("creating a session", () => {
    it("calls streamCreateSession and shows the problem after selecting difficulty", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("Write a function that adds two numbers.")).toBeInTheDocument();
      });

      expect(vi.mocked(apiClient.streamCreateSession).mock.calls[0]?.[0]).toEqual({ difficulty: "Easy", language: "CSharp", provider: "Anthropic", focus: "Random", topic: "Random" });
    });

    it("shows the description streaming while the problem is being written", async () => {
      const user = userEvent.setup();
      let resolveFinal!: (session: ProblemSession) => void;
      vi.mocked(apiClient.streamCreateSession).mockImplementation((_body, callbacks) => {
        callbacks.onDelta("Write a function");
        return new Promise<ProblemSession>((resolve) => {
          resolveFinal = resolve;
        });
      });

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByTestId("streaming-description")).toBeInTheDocument();
      });
      expect(screen.getByText("Write a function")).toBeInTheDocument();

      resolveFinal(mockSession);
      await waitFor(() => {
        expect(screen.getByText("Write a function that adds two numbers.")).toBeInTheDocument();
      });
    });

    it("displays the starter code after session creation", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("public int Add(int a, int b) {}")).toBeInTheDocument();
      });
    });

    it("shows error message when session creation fails", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockRejectedValue(new Error("API unavailable"));

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("API unavailable")).toBeInTheDocument();
      });
    });
  });

  describe("after session is created", () => {
    async function renderWithSession() {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("Write a function that adds two numbers.")).toBeInTheDocument();
      });

      return user;
    }

    it("renders the chat input", async () => {
      await renderWithSession();

      expect(screen.getByPlaceholderText("Ask for guidance...")).toBeInTheDocument();
    });

    it("displays the difficulty badge", async () => {
      await renderWithSession();

      expect(screen.getByText("Easy")).toBeInTheDocument();
    });

    it("renders a draggable separator between the code and chat panels", async () => {
      await renderWithSession();

      const separator = screen.getByRole("separator");
      expect(separator).toBeInTheDocument();
      expect(separator).toHaveAttribute("aria-orientation", "vertical");
    });

    it("shows user message immediately after sending", async () => {
      vi.mocked(apiClient.streamChat).mockResolvedValue(mockChatResponse);
      const user = await renderWithSession();

      const input = screen.getByPlaceholderText("Ask for guidance...");
      await user.type(input, "How do I start?{Enter}");

      expect(screen.getByText("How do I start?")).toBeInTheDocument();
    });

    it("shows assistant response after sending a message", async () => {
      vi.mocked(apiClient.streamChat).mockResolvedValue(mockChatResponse);
      const user = await renderWithSession();

      const input = screen.getByPlaceholderText("Ask for guidance...");
      await user.type(input, "How do I start?{Enter}");

      await waitFor(() => {
        expect(screen.getByText("Try a for loop")).toBeInTheDocument();
      });

      const sendCall = vi.mocked(apiClient.streamChat).mock.calls[0];
      expect(sendCall?.[0]).toBe("test-session-id");
      expect(sendCall?.[1]).toEqual({ message: "How do I start?", editorContent: "public int Add(int a, int b) {}", guidanceMode: "Guidance" });
    });

    it("renders the reply as it streams, before the final response arrives", async () => {
      let resolveFinal!: (response: ChatResponse) => void;
      vi.mocked(apiClient.streamChat).mockImplementation((_id, _body, callbacks) => {
        callbacks.onDelta("Try a ");
        callbacks.onDelta("for loop");
        return new Promise<ChatResponse>((resolve) => {
          resolveFinal = resolve;
        });
      });
      const user = await renderWithSession();

      await user.type(screen.getByPlaceholderText("Ask for guidance..."), "How do I start?{Enter}");

      await waitFor(() => {
        expect(screen.getByText("Try a for loop")).toBeInTheDocument();
      });

      resolveFinal(mockChatResponse);
      await waitFor(() => {
        expect(screen.getByText("Try a for loop")).toBeInTheDocument();
      });
    });

    it("keeps the partial reply with an error and restores the message when the stream dies", async () => {
      vi.mocked(apiClient.streamChat).mockImplementation(async (_id, _body, callbacks) => {
        callbacks.onDelta("half a hint");
        throw new Error("AI service error");
      });
      const user = await renderWithSession();

      const input = screen.getByPlaceholderText("Ask for guidance...");
      await user.type(input, "How do I start?{Enter}");

      await waitFor(() => {
        expect(screen.getByTestId("failed-turn")).toBeInTheDocument();
      });
      expect(screen.getByText("half a hint")).toBeInTheDocument();          // partial stays visible, dimmed
      expect(input).toHaveValue("How do I start?");                          // message restored for resend
      expect(screen.queryByText("How do I start?")).not.toBeInTheDocument(); // user bubble rolled back
    });
  });

  // == Focus and Topic == //

  describe("focus and topic", () => {
    async function startSession(route = "/") {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow(route);
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("Write a function that adds two numbers.")).toBeInTheDocument();
      });

      return user;
    }

    it("renders resolved focus and topic badges in the session row", async () => {
      await startSession();

      // The session's resolved values, not the user's "Random" selection
      expect(screen.getByText("Refactoring")).toBeInTheDocument();
      expect(screen.getByText("State machines")).toBeInTheDocument();
    });

    it("renders a Standard badge when focus resolves to Standard", async () => {
      // Standard is a real option, not the absence of one — it badges like any other focus
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue({ ...mockSession, focus: "Standard" });
      const user = userEvent.setup();
      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("Standard implementation")).toBeInTheDocument();
      });
    });

    it("sends the user's selection, not the session's resolved values, when regenerating", async () => {
      // The load-bearing one: the session came back Refactoring, but the selection was Random, so
      // regenerating must re-roll rather than silently pin to the first roll.
      const user = await startSession();

      await user.click(screen.getByRole("button", { name: /new problem/i }));

      await waitFor(() => expect(vi.mocked(apiClient.streamCreateSession)).toHaveBeenCalledTimes(2));
      expect(requestBody(1)).toMatchObject({ focus: "Random", topic: "Random" });
    });

    it("repeats a pinned focus when regenerating", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);
      renderChatWindow();

      await user.selectOptions(screen.getByLabelText("Focus"), "Refactoring");
      await user.click(screen.getByRole("button", { name: "Easy" }));
      await waitFor(() => {
        expect(screen.getByText("Write a function that adds two numbers.")).toBeInTheDocument();
      });

      await user.click(screen.getByRole("button", { name: /new problem/i }));

      await waitFor(() => expect(vi.mocked(apiClient.streamCreateSession)).toHaveBeenCalledTimes(2));
      expect(requestBody(1)).toMatchObject({ focus: "Refactoring" });
    });

    it("seeds the selection from ?focus= and ?topic= params", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow("/?focus=BugFix&topic=BitManipulation");
      await user.click(screen.getByRole("button", { name: "Medium" }));

      await waitFor(() => expect(vi.mocked(apiClient.streamCreateSession)).toHaveBeenCalled());
      expect(requestBody()).toMatchObject({ focus: "BugFix", topic: "BitManipulation" });
    });

    it("falls back to Random when the focus and topic params are malformed", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow("/?focus=NotAFocus&topic=__proto__");
      await user.click(screen.getByRole("button", { name: "Medium" }));

      await waitFor(() => expect(vi.mocked(apiClient.streamCreateSession)).toHaveBeenCalled());
      expect(requestBody()).toMatchObject({ focus: "Random", topic: "Random" });
    });

    it("auto-starts from lang and difficulty alone, defaulting focus and topic to Random", async () => {
      // Backward-compatibility guard: every pre-existing bookmark must behave exactly as before
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow("/?lang=Python&difficulty=Hard");

      await waitFor(() => expect(vi.mocked(apiClient.streamCreateSession)).toHaveBeenCalled());
      expect(requestBody()).toEqual({
        difficulty: "Hard",
        language: "Python",
        provider: "Anthropic",
        focus: "Random",
        topic: "Random",
      });
    });

    it("carries focus and topic through an auto-start", async () => {
      vi.mocked(apiClient.streamCreateSession).mockResolvedValue(mockSession);

      renderChatWindow("/?lang=Python&difficulty=Hard&focus=Refactoring");

      await waitFor(() => expect(vi.mocked(apiClient.streamCreateSession)).toHaveBeenCalled());
      expect(requestBody()).toMatchObject({ focus: "Refactoring", topic: "Random" });
    });
  });
});
