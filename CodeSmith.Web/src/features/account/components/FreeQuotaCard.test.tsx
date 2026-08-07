// == Free-quota card: active bar, exhausted line, IP notice == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import * as apiClient from "../../../lib/apiClient";
import type { QuotaResponse } from "../types";
import { FreeQuotaCard } from "./FreeQuotaCard";
import { AccountWalletRow } from "./AccountWalletRow";

vi.mock("../../../lib/apiClient");

function renderWithQuota(quota: QuotaResponse | Promise<QuotaResponse> | Error) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  if (quota instanceof Error) {
    vi.mocked(apiClient.getQuota).mockRejectedValue(quota);
  } else {
    vi.mocked(apiClient.getQuota).mockResolvedValue(quota as QuotaResponse);
  }
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  return { ...render(<FreeQuotaCard />, { wrapper }), queryClient };
}

function renderWalletRow(quota: QuotaResponse) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  vi.mocked(apiClient.getQuota).mockResolvedValue(quota);
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  return render(
    <AccountWalletRow
      credits={<div data-testid="credits-slot">Credits body</div>}
    />,
    { wrapper }
  );
}

beforeEach(() => {
  vi.mocked(apiClient.getQuota).mockReset();
});

describe("FreeQuotaCard", () => {
  it("renders visible used / max token counts and a flat accent fill at 85% usage (no red ramp)", async () => {
    // 17_000 / 20_000 = 85%
    renderWithQuota({ freeTokensUsed: 17_000, freeQuotaMax: 20_000, ipConstraint: "None" });

    expect(await screen.findByText(/17,000\s*\/\s*20,000/)).toBeInTheDocument();

    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill).not.toBeNull();
    expect(fill.style.width).toBe("85%");
    expect(fill.className).toContain("bg-accent");
    expect(fill.className).not.toContain("bg-red-500");
    expect(fill.className).not.toContain("bg-yellow-400");
    expect(fill.className).not.toContain("bg-emerald-500");
  });

  it("renders the exhausted muted line with no card chrome", async () => {
    renderWithQuota({ freeTokensUsed: 20_000, freeQuotaMax: 20_000, ipConstraint: "None" });

    expect(
      await screen.findByText(/Free tokens — 20,000 of 20,000 used/)
    ).toBeInTheDocument();
    expect(screen.queryByTestId("account-section-free-quota")).not.toBeInTheDocument();
    // No fill bar in exhausted state
    expect(document.querySelector("[style]")).toBeNull();
  });

  it("zero-usage account renders the bar at min-fill with visible 0 / max and no special-case copy", async () => {
    renderWithQuota({ freeTokensUsed: 0, freeQuotaMax: 20_000, ipConstraint: "None" });

    expect(await screen.findByText(/0\s*\/\s*20,000/)).toBeInTheDocument();
    expect(screen.queryByText(/haven't|not started|trial|welcome/i)).not.toBeInTheDocument();

    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.style.width).toBe("0.3%"); // Shared min-fill invariant (pct is 0)
    expect(screen.getByTestId("account-section-free-quota")).toBeInTheDocument();
  });

  it("shows the IP notice for Limited and Exhausted without a per-IP number, and omits it for None", async () => {
    const { unmount } = renderWithQuota({
      freeTokensUsed: 1_000,
      freeQuotaMax: 20_000,
      ipConstraint: "Limited",
    });
    expect(await screen.findByTestId("free-quota-ip-notice")).toBeInTheDocument();
    expect(screen.getByTestId("free-quota-ip-notice").textContent).not.toMatch(/\d/);
    unmount();

    const second = renderWithQuota({
      freeTokensUsed: 1_000,
      freeQuotaMax: 20_000,
      ipConstraint: "Exhausted",
    });
    expect(await screen.findByTestId("free-quota-ip-notice")).toBeInTheDocument();
    expect(screen.getByTestId("free-quota-ip-notice").textContent).not.toMatch(/\d/);
    second.unmount();

    renderWithQuota({ freeTokensUsed: 1_000, freeQuotaMax: 20_000, ipConstraint: "None" });
    await screen.findByText(/1,000\s*\/\s*20,000/);
    expect(screen.queryByTestId("free-quota-ip-notice")).not.toBeInTheDocument();
  });
});

describe("AccountWalletRow", () => {
  it("collapses to a single full-width column when the free grant is exhausted", async () => {
    renderWalletRow({ freeTokensUsed: 20_000, freeQuotaMax: 20_000, ipConstraint: "None" });

    const row = await screen.findByTestId("account-wallet-row");
    await waitFor(() => {
      expect(row.className).toContain("grid-cols-1");
      expect(row.className).not.toContain("sm:grid-cols-2");
    });
    expect(screen.getByTestId("credits-slot")).toBeInTheDocument();
    expect(screen.getByText(/Free tokens — 20,000 of 20,000 used/)).toBeInTheDocument();
  });

  it("keeps the two-column wallet row while the grant is active", async () => {
    renderWalletRow({ freeTokensUsed: 100, freeQuotaMax: 20_000, ipConstraint: "None" });

    const row = await screen.findByTestId("account-wallet-row");
    await waitFor(() => {
      expect(row.className).toContain("sm:grid-cols-2");
    });
    expect(screen.getByTestId("account-section-free-quota")).toBeInTheDocument();
  });
});
