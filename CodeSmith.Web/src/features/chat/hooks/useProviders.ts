// == Providers Query Hook == //
import { useQuery } from "@tanstack/react-query";
import { getProviders } from "../../../lib/apiClient";
import type { ProvidersResponse } from "../types";

export function useProviders() {
  return useQuery<ProvidersResponse, Error>({
    queryKey: ["providers"],
    queryFn: getProviders,
    staleTime: Infinity,  // Provider config does not change at runtime
    // Cold Container App 502s are ordinary; retry hard on first fetch (staleTime: Infinity)
    retry: 3,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 8000),
  });
}
