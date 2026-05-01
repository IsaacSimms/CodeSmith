// == Providers Query Hook == //
import { useQuery } from "@tanstack/react-query";
import { getProviders } from "../../../lib/apiClient";
import type { ProvidersResponse } from "../types";

export function useProviders() {
  return useQuery<ProvidersResponse, Error>({
    queryKey: ["providers"],
    queryFn: getProviders,
    staleTime: Infinity,  // Provider config does not change at runtime
  });
}
