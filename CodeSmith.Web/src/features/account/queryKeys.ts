// == Shared TanStack keys — nav dropdown and account page must share one cache == //

export const accountQueryKeys = {
  quota: ["usage", "quota"] as const,
  balance: ["billing", "balance"] as const,
  ledger: ["billing", "ledger"] as const,
  packs: ["billing", "packs"] as const,
};
