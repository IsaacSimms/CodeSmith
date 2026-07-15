// == Home Page Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { HomePage } from "./HomePage";

// API returns enum declaration order (Anthropic, OpenAi, Xai); UI reorders for display.
vi.mock("../../chat/hooks/useProviders", () => ({
  useProviders: () => ({
    data: {
      activeProvider: "Xai",
      availableProviders: ["Anthropic", "OpenAi", "Xai"],
    },
  }),
}));

vi.mock("../../../hooks/useProviderPreference", () => ({
  useProviderPreference: () => ({ provider: "Xai", setProvider: vi.fn() }),
}));

function renderHomePage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <HomePage />
      </MemoryRouter>
    </QueryClientProvider>
  );
}

describe("HomePage", () => {
  it("renders the hero heading", () => {
    renderHomePage();
    expect(
      screen.getByRole("heading", { name: /forge the skill\. then prove it\./i })
    ).toBeInTheDocument();
  });

  it("renders the hero subtext", () => {
    renderHomePage();
    expect(
      screen.getByText(/multiple disciplines\. one habit: deliberate practice\./i)
    ).toBeInTheDocument();
  });


  it("renders a CTA link pointing to /pairedprogrammer", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /paired programmer/i });
    expect(link).toHaveAttribute("href", "/pairedprogrammer");
  });

  it("renders a CTA link pointing to /prompt-lab", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /prompt lab/i });
    expect(link).toHaveAttribute("href", "/prompt-lab");
  });

  it("renders a CTA link pointing to /system-lab", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /system lab/i });
    expect(link).toHaveAttribute("href", "/system-lab");
  });

  it("renders provider buttons as Anthropic, xAI, OpenAI regardless of API order", () => {
    renderHomePage();
    const buttons = screen.getAllByRole("button");
    expect(buttons.map((b) => b.textContent)).toEqual(["Anthropic", "xAI", "OpenAI"]);
  });
});
