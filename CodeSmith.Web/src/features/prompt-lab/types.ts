// == Prompt Lab Feature Types == //
import type { Difficulty } from "../chat/types";

export type { Difficulty };

export type ChallengeCategory =
  | "OutputFormatControl"
  | "SpecificityOfScope"
  | "NegativeInstructions"
  | "ConditionalBehavior"
  | "QuantityEnumeration"
  | "ToneRegister";

export type PromptFieldType = "SystemPrompt" | "UserMessage";

// == Challenge Catalog Types == //

export interface EditableField {
  fieldType: PromptFieldType;
  placeholder: string;
  defaultValue: string;
}

export interface TestInputSummary {
  inputId: string;
  label: string;
}

export interface RubricCriterion {
  criterionId: string;
  name: string;
  description: string;
  maxPoints: number;
}

export interface ChallengeResponse {
  challengeId: string;
  title: string;
  description: string;
  category: ChallengeCategory;
  difficulty: Difficulty;
  lockedSystemPrompt: string;
  editableFields: EditableField[];
  testInputs: TestInputSummary[];
  rubric: RubricCriterion[];
}

// == Session and Attempt Types == //

export interface CriterionScore {
  criterionId: string;
  criterionName: string;
  points: number;
  maxPoints: number;
}

export interface TestInputResult {
  inputId: string;
  label: string;
  simulationOutput: string;
  passed: boolean;
  criterionScores: CriterionScore[];
  feedback: string;
}

export interface AttemptResult {
  attemptId: string;
  totalScore: number;
  maxScore: number;
  overallFeedback: string;
  results: TestInputResult[];
  submittedAt: string;
  promptTokensUsed: number;  // Input tokens for one simulation call — representative prompt size
  contextWindowSize: number; // Model context window limit (200,000 for all current models)
}

export interface PromptLabSession {
  sessionId: string;
  challengeId: string;
  testInputs: TestInputSummary[];  // Dynamically generated at session start
  attempts: AttemptResult[];
  createdAt: string;
}

// == Request Types == //

export interface StartChallengeRequest {
  challengeId: string;
}

export interface SubmitAttemptRequest {
  systemPromptContent: string;
  userMessageContent: string;
}

// == Display Helpers == //

export const categoryLabels: Record<ChallengeCategory, string> = {
  OutputFormatControl:  "Output Format Control",
  SpecificityOfScope:   "Specificity of Scope",
  NegativeInstructions: "Negative Instructions",
  ConditionalBehavior:  "Conditional Behavior",
  QuantityEnumeration:  "Quantity & Enumeration",
  ToneRegister:         "Tone & Register",
};
