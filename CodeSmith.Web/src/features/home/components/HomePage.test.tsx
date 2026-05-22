// == Home Page Tests == //
import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { HomePage } from "./HomePage";

vi.mock("../../chat/hooks/useProviders", () => ({
  useProviders: () => ({ data: { activeProvider: "Anthropic", availableProviders: ["Anthropic"] } }),
}));

vi.mock("../../../hooks/useProviderPreference", () => ({
  useProviderPreference: () => ({ provider: "Anthropic", setProvider: vi.fn() }),
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
      screen.getByRole("heading", { name: /practice\. learn\. level up\./i })
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
});
