// == Transaction history: filterable ledger list == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router-dom";
import * as apiClient from "../../../lib/apiClient";
import type { LedgerEntryResponse } from "../types";
import { TransactionHistorySection } from "./TransactionHistorySection";

vi.mock("../../../lib/apiClient");

const sampleRows: LedgerEntryResponse[] = [
  {
    type: "Spend",
    amountUsd: 0,
    isFreeCovered: true,
    feature: "Tutoring:Guidance",
    timestampUtc: "2026-08-01T12:00:00Z",
  },
  {
    type: "Spend",
    amountUsd: 0.0042,
    isFreeCovered: false,
    feature: "PromptLab:Evaluate",
    timestampUtc: "2026-08-02T12:00:00Z",
  },
  {
    type: "TopUp",
    amountUsd: 10,
    isFreeCovered: false,
    feature: "Billing:TopUp",
    timestampUtc: "2026-08-03T12:00:00Z",
  },
];

function renderSection(rows: LedgerEntryResponse[] = sampleRows) {
  vi.mocked(apiClient.getLedger).mockResolvedValue(rows);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>{children}</MemoryRouter>
    </QueryClientProvider>
  );
  return render(<TransactionHistorySection />, { wrapper });
}

beforeEach(() => {
  vi.mocked(apiClient.getLedger).mockReset();
});

describe("TransactionHistorySection", () => {
  it("renders rows with date, feature label, and amount; All is selected on load", async () => {
    renderSection();

    // Wait for ledger data (chips live in the loaded body, not the loading shell)
    const list = await screen.findByTestId("ledger-list");
    expect(screen.getByTestId("account-section-history")).toBeInTheDocument();

    // Filter chips present; All is the default selection
    const allChip = screen.getByRole("button", { name: "All" });
    const purchasesChip = screen.getByRole("button", { name: "Purchases" });
    const usageChip = screen.getByRole("button", { name: "Usage" });
    expect(allChip).toHaveAttribute("aria-pressed", "true");
    expect(purchasesChip).toHaveAttribute("aria-pressed", "false");
    expect(usageChip).toHaveAttribute("aria-pressed", "false");

    // All three rows visible under All
    const rows = within(list).getAllByTestId("ledger-row");
    expect(rows).toHaveLength(3);

    // Mapped feature label
    expect(list).toHaveTextContent("Paired Programmer · Guidance");
    expect(list).toHaveTextContent("Prompt Lab · Evaluate");
    expect(list).toHaveTextContent("Purchase");

    // Dates present (UTC short form)
    expect(list).toHaveTextContent("Aug 1, 2026");
    expect(list).toHaveTextContent("Aug 2, 2026");
    expect(list).toHaveTextContent("Aug 3, 2026");
  });

  it("renders isFreeCovered rows as Free and includes them under All and Usage", async () => {
    const user = userEvent.setup();
    renderSection();

    const list = await screen.findByTestId("ledger-list");
    // Free amount slot — not a currency figure
    expect(list).toHaveTextContent("Free");
    expect(list).not.toHaveTextContent("$0.0000");

    // Free row is Spend → still visible under Usage
    await user.click(screen.getByRole("button", { name: "Usage" }));
    expect(screen.getByRole("button", { name: "Usage" })).toHaveAttribute("aria-pressed", "true");

    const usageList = screen.getByTestId("ledger-list");
    const usageRows = within(usageList).getAllByTestId("ledger-row");
    expect(usageRows).toHaveLength(2); // free Spend + paid Spend
    expect(usageList).toHaveTextContent("Free");
    expect(usageList).toHaveTextContent("Paired Programmer · Guidance");
    expect(usageList).not.toHaveTextContent("Purchase"); // TopUp filtered out

    // Purchases excludes the free Usage row
    await user.click(screen.getByRole("button", { name: "Purchases" }));
    const purchasesList = screen.getByTestId("ledger-list");
    expect(within(purchasesList).getAllByTestId("ledger-row")).toHaveLength(1);
    expect(purchasesList).toHaveTextContent("Purchase");
    expect(purchasesList).not.toHaveTextContent("Free");
  });

  it("formats Spend at 4dp and TopUp at 2dp", async () => {
    renderSection();

    const list = await screen.findByTestId("ledger-list");
    expect(list).toHaveTextContent("$0.0042");
    expect(list).toHaveTextContent("$10.00");
    // TopUp must not pad to 4dp
    expect(list).not.toHaveTextContent("$10.0000");
  });

  it("falls back to the raw Feature string for unmapped values", async () => {
    renderSection([
      {
        type: "Spend",
        amountUsd: 0.01,
        isFreeCovered: false,
        feature: "FutureSurface:NewAction",
        timestampUtc: "2026-08-04T12:00:00Z",
      },
    ]);

    const list = await screen.findByTestId("ledger-list");
    expect(list).toHaveTextContent("FutureSurface:NewAction");
    // Must not blank out
    expect(within(list).getByTestId("ledger-row").textContent).toMatch(/FutureSurface:NewAction/);
  });

  it("empty ledger still shows chips and an empty state, not an error", async () => {
    renderSection([]);

    // Chips present even with zero rows
    expect(await screen.findByRole("button", { name: "All" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Purchases" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Usage" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "All" })).toHaveAttribute("aria-pressed", "true");

    expect(screen.getByTestId("ledger-empty")).toHaveTextContent(/no transactions/i);
    expect(screen.queryByTestId("ledger-list")).not.toBeInTheDocument();
    expect(screen.queryByTestId("failure-notice")).not.toBeInTheDocument();
  });
});
