// == System Lab Feature Types == //
import type { AiProvider, Difficulty } from "../chat/types";

export type { AiProvider, Difficulty };

export type SystemLabCategory =
  | "IdentityAndGovernance"
  | "Compute"
  | "Storage"
  | "NetworkingAndConnectivity"
  | "ResilienceAndContinuity"
  | "MonitoringAndObservability"
  | "AutomationAndIaC";

export type EvaluationMode = "SingleAnswer" | "TradeoffReasoning" | "OpenJudgment";

// == Scenario Catalog Types == //

export interface ScenarioRubricCriterion {
  criterionId: string;
  name: string;
  description: string;
  maxPoints: number;
}

export interface ScenarioResponse {
  scenarioId: string;
  title: string;
  description: string;
  constraints: string;
  category: SystemLabCategory;
  difficulty: Difficulty;
  evaluationMode: EvaluationMode;
  rubric: ScenarioRubricCriterion[];
  requiredTradeoffs: string[];
}

// == Session and Attempt Types == //

export interface CriterionScore {
  criterionId: string;
  criterionName: string;
  points: number;
  maxPoints: number;
}

export interface TradeoffResult {
  tradeoffQuestion: string;
  engaged: boolean;
  feedback: string;
}

export interface DimensionDeduction {
  dimensionName: string;
  deduction: number;
  feedback: string | null;
}

export interface AttemptResult {
  attemptId: string;
  rubricScore: number;
  maxRubricScore: number;
  dimensionDeductions: DimensionDeduction[];
  totalScore: number;
  maxScore: number;
  overallFeedback: string;
  criterionScores: CriterionScore[];
  tradeoffResults: TradeoffResult[];
  submittedAt: string;
}

export interface SystemLabSession {
  sessionId: string;
  scenarioId: string;
  attempts: AttemptResult[];
  createdAt: string;
}

// == Request Types == //

export interface StartSessionRequest {
  scenarioId: string;
  provider?: AiProvider;
}

export interface SubmitJustificationRequest {
  justificationContent: string;
}

export interface SystemLabChatRequest {
  message: string;
  currentJustification?: string;
}

export interface SystemLabChatResponse {
  response: string;
}

// == Display Helpers == //

export const categoryLabels: Record<SystemLabCategory, string> = {
  IdentityAndGovernance:     "Identity & Governance",
  Compute:                   "Compute",
  Storage:                   "Storage",
  NetworkingAndConnectivity: "Networking & Connectivity",
  ResilienceAndContinuity:   "Resilience & Continuity",
  MonitoringAndObservability:"Monitoring & Observability",
  AutomationAndIaC:          "Automation & IaC",
};

export const evaluationModeLabels: Record<EvaluationMode, string> = {
  SingleAnswer:      "Single Answer",
  TradeoffReasoning: "Tradeoff Reasoning",
  OpenJudgment:      "Open Judgment",
};
