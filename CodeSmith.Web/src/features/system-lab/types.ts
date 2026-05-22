// == System Lab Feature Types == //
import type { Difficulty } from "../chat/types";

export type { Difficulty };

export type SystemLabCategory =
  | "IdentityAndGovernance"
  | "Compute"
  | "Storage"
  | "NetworkingAndConnectivity"
  | "ResilienceAndContinuity"
  | "MonitoringAndOperations"
  | "CostAndCapacity"
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

export interface AttemptResult {
  attemptId: string;
  rubricScore: number;
  maxRubricScore: number;
  securityDeduction: number;
  totalScore: number;
  maxScore: number;
  overallFeedback: string;
  securityFeedback: string | null;
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

// == Local Chat Display Type == //

export interface SystemLabChatMessage {
  role: "user" | "assistant";
  content: string;
}

// == Display Helpers == //

export const categoryLabels: Record<SystemLabCategory, string> = {
  IdentityAndGovernance:    "Identity & Governance",
  Compute:                  "Compute",
  Storage:                  "Storage",
  NetworkingAndConnectivity:"Networking & Connectivity",
  ResilienceAndContinuity:  "Resilience & Continuity",
  MonitoringAndOperations:  "Monitoring & Operations",
  CostAndCapacity:          "Cost & Capacity",
  AutomationAndIaC:         "Automation & IaC",
};

export const evaluationModeLabels: Record<EvaluationMode, string> = {
  SingleAnswer:      "Single Answer",
  TradeoffReasoning: "Tradeoff Reasoning",
  OpenJudgment:      "Open Judgment",
};
