// == Chat Window Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MemoryRouter } from "react-router-dom";
import { ChatWindow } from "./ChatWindow";
import * as apiClient from "../../../lib/apiClient";
import type { ProblemSession } from "../types";

vi.mock("../../../lib/apiClient");

const mockSession: ProblemSession = {
  sessionId: "test-session-id",
  difficulty: "Easy",
  problemDescription: "Write a function that adds two numbers.",
  starterCode: "public int Add(int a, int b) {}",
  messages: [],
  createdAt: "2026-03-31T00:00:00Z",
};

function renderChatWindow() {
  const queryClient = new QueryClient({
    defaultOptions: { mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <ChatWindow />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

beforeEach(() => {
  vi.restoreAllMocks();
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
    it("calls createSession and shows the problem after selecting difficulty", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.createSession).mockResolvedValue(mockSession);

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("Write a function that adds two numbers.")).toBeInTheDocument();
      });

      expect(vi.mocked(apiClient.createSession).mock.calls[0]?.[0]).toEqual({ difficulty: "Easy" });
    });

    it("displays the starter code after session creation", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.createSession).mockResolvedValue(mockSession);

      renderChatWindow();
      await user.click(screen.getByRole("button", { name: "Easy" }));

      await waitFor(() => {
        expect(screen.getByText("public int Add(int a, int b) {}")).toBeInTheDocument();
      });
    });

    it("shows error message when session creation fails", async () => {
      const user = userEvent.setup();
      vi.mocked(apiClient.createSession).mockRejectedValue(new Error("API unavailable"));

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
      vi.mocked(apiClient.createSession).mockResolvedValue(mockSession);

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

    it("shows user message immediately after sending", async () => {
      vi.mocked(apiClient.sendMessage).mockResolvedValue({ response: "Try a for loop" });
      const user = await renderWithSession();

      const input = screen.getByPlaceholderText("Ask for guidance...");
      await user.type(input, "How do I start?{Enter}");

      expect(screen.getByText("How do I start?")).toBeInTheDocument();
    });

    it("shows assistant response after sending a message", async () => {
      vi.mocked(apiClient.sendMessage).mockResolvedValue({ response: "Try a for loop" });
      const user = await renderWithSession();

      const input = screen.getByPlaceholderText("Ask for guidance...");
      await user.type(input, "How do I start?{Enter}");

      await waitFor(() => {
        expect(screen.getByText("Try a for loop")).toBeInTheDocument();
      });

      const sendCall = vi.mocked(apiClient.sendMessage).mock.calls[0];
      expect(sendCall?.[0]).toBe("test-session-id");
      expect(sendCall?.[1]).toEqual({ message: "How do I start?" });
    });
  });
});
