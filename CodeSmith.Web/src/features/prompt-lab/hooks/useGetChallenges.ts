// == Get Challenges Hook == //
import { useQuery } from "@tanstack/react-query";
import { getChallenges } from "../../../lib/apiClient";
import type { ChallengeResponse } from "../types";

export function useGetChallenges() {
  return useQuery<ChallengeResponse[], Error>({
    queryKey: ["prompt-lab", "challenges"],
    queryFn: getChallenges,
  });
}
