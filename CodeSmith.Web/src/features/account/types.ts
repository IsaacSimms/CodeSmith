// == Account / billing read models (mirror server DTOs, camelCase JSON) == //

export type IpConstraint = "None" | "Limited" | "Exhausted";

export interface QuotaResponse {
  freeTokensUsed: number;
  freeQuotaMax: number;
  ipConstraint: IpConstraint;
}

export interface BalanceResponse {
  paidCreditsUsd: number;
}

export type LedgerEntryType = "TopUp" | "Spend";

export interface LedgerEntryResponse {
  type: LedgerEntryType;
  amountUsd: number;
  isFreeCovered: boolean; // True only for fully free-covered Spend rows
  feature: string | null;
  timestampUtc: string;
}

export interface PackResponse {
  priceId: string;
  name: string;
  amount: number;
  currency: string;
}
