// == Get Scenarios Hook == //
import { useQuery } from "@tanstack/react-query";
import { getScenarios } from "../../../lib/apiClient";
import type { ScenarioResponse } from "../types";

export function useGetScenarios() {
  return useQuery<ScenarioResponse[], Error>({
    queryKey: ["system-lab-scenarios"],
    queryFn: getScenarios,
  });
}
