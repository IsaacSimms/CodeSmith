// == Transaction history: filterable ledger list == //
import { useState } from "react";
import { useLedger } from "../hooks/useLedger";
import { formatLedgerUsd, ledgerFeatureLabel } from "../formatters";
import { interpretError } from "../../../lib/clientError";
import type { LedgerEntryResponse } from "../types";
import { AccountSection } from "./AccountSection";

type LedgerFilter = "All" | "Purchases" | "Usage";

const FILTERS: LedgerFilter[] = ["All", "Purchases", "Usage"];

// Chip → LedgerEntryType 1:1. Free-covered Spend is still Usage (ticket 008 #9).
function matchesFilter(row: LedgerEntryResponse, filter: LedgerFilter): boolean {
  if (filter === "All") return true;
  if (filter === "Purchases") return row.type === "TopUp";
  return row.type === "Spend"; // Usage
}

// UTC so SSR / local timezone never flip the calendar day for ledger timestamps.
function formatLedgerDate(iso: string): string {
  return new Date(iso).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: "UTC",
  });
}

function amountLabel(row: LedgerEntryResponse): string {
  // Server owns the free rule — never derive from amountUsd === 0 (ticket 008 #4, #8)
  if (row.isFreeCovered) return "Free";
  return formatLedgerUsd(row.amountUsd, row.type);
}

/// Owns the History AccountSection + client-side All/Purchases/Usage chips over useLedger.
export function TransactionHistorySection() {
  const ledger = useLedger();
  const [filter, setFilter] = useState<LedgerFilter>("All");

  const error = ledger.error ? interpretError(ledger.error) : null;
  const rows = (ledger.data ?? []).filter((r) => matchesFilter(r, filter));

  return (
    <AccountSection
      title="History"
      anchorId="history"
      isLoading={ledger.isLoading}
      error={error}
    >
      <div className="flex flex-col gap-4">
        <FilterChips selected={filter} onSelect={setFilter} />

        {rows.length === 0 ? (
          <p data-testid="ledger-empty" className="text-sm text-gray-400">
            No transactions yet.
          </p>
        ) : (
          <ul data-testid="ledger-list" className="divide-y divide-gray-800">
            {rows.map((row, i) => (
              <LedgerRow key={`${row.timestampUtc}-${row.type}-${i}`} row={row} />
            ))}
          </ul>
        )}
      </div>
    </AccountSection>
  );
}

// == Filter chips: All / Purchases / Usage (client-side over one query) == //
function FilterChips({
  selected,
  onSelect,
}: {
  selected: LedgerFilter;
  onSelect: (f: LedgerFilter) => void;
}) {
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label="Transaction filters">
      {FILTERS.map((f) => {
        const isSelected = selected === f;
        return (
          <button
            key={f}
            type="button"
            aria-pressed={isSelected}
            onClick={() => onSelect(f)}
            className={`rounded-full border px-3 py-1 text-sm font-medium transition-colors ${
              isSelected
                ? "border-accent bg-accent/20 text-white"
                : "border-gray-600 bg-gray-800 text-gray-300 hover:border-gray-500 hover:bg-gray-700"
            }`}
          >
            {f}
          </button>
        );
      })}
    </div>
  );
}

// == Single ledger line: date · feature · amount (stacks below sm) == //
function LedgerRow({ row }: { row: LedgerEntryResponse }) {
  return (
    <li
      data-testid="ledger-row"
      className="flex flex-col gap-0.5 py-3 text-sm sm:flex-row sm:items-center sm:gap-3"
    >
      <span className="shrink-0 text-gray-400 tabular-nums">{formatLedgerDate(row.timestampUtc)}</span>
      <span className="min-w-0 flex-1 text-gray-200">{ledgerFeatureLabel(row.feature)}</span>
      <span className="shrink-0 text-right font-medium tabular-nums text-white sm:ml-auto">
        {amountLabel(row)}
      </span>
    </li>
  );
}
