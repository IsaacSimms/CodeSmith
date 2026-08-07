// == Client Failure Interpretation == //

export type ClientFailureKind = "paywall" | "login" | "notFound" | "ai" | "generic";

export interface ClientFailureAction {
  label: string;
  href: string;
}

export interface ClientFailure {
  kind: ClientFailureKind;
  title: string;
  detail: string;
  action?: ClientFailureAction; // Optional CTA — interpretError sets for paywall / login
}

// Fixed SPA copy — product wording, not raw ProblemDetails.detail.
const COPY: Record<ClientFailureKind, { title: string; detail: string; action?: ClientFailureAction }> = {
  paywall: {
    title: "Out of free quota and credits",
    detail:
      "You don't have enough remaining free usage or purchased credits for this request.",
    action: { label: "Add credits", href: "/account#credits" },
  },
  login: {
    title: "Sign in required",
    detail: "Sign in with an account to use AI features.",
    action: { label: "Sign in", href: "/account" },
  },
  notFound: {
    title: "Not found",
    detail: "That session or resource is no longer available.",
  },
  ai: {
    title: "AI service error",
    detail: "Something went wrong talking to the model. Try again in a moment.",
  },
  generic: {
    title: "Something went wrong",
    detail: "An unexpected error occurred. Please try again.",
  },
};

// Duck-type ApiClientError so interpretError works when tests mock the apiClient module
// (instanceof against a second class identity would fail).
interface ApiClientLike {
  statusCode: number;
  message?: string;
  apiError?: { title?: string; detail?: string; status?: number; code?: string };
}

function isApiClientLike(error: unknown): error is ApiClientLike {
  return (
    typeof error === "object" &&
    error !== null &&
    "statusCode" in error &&
    typeof (error as ApiClientLike).statusCode === "number"
  );
}

// == interpretError == //

// Maps any thrown value (usually ApiClientError) to a ClientFailure for FailureNotice.
export function interpretError(error: unknown): ClientFailure {
  if (isApiClientLike(error)) {
    const kind = kindFromStatus(error.statusCode, error.apiError);
    if (kind === "generic") {
      const detail = error.message?.trim() || COPY.generic.detail;
      return { kind, title: COPY.generic.title, detail };
    }
    const copy = COPY[kind];
    return {
      kind,
      title: copy.title,
      detail: copy.detail,
      ...(copy.action ? { action: copy.action } : {}),
    };
  }

  if (error instanceof Error && error.message.trim()) {
    return { kind: "generic", title: COPY.generic.title, detail: error.message };
  }

  return { kind: "generic", title: COPY.generic.title, detail: COPY.generic.detail };
}

function kindFromStatus(
  status: number,
  apiError?: { code?: string; title?: string } | null
): ClientFailureKind {
  if (status === 402) return "paywall";
  if (status === 401) return "login";
  if (status === 404) return "notFound";
  if (status === 502 || status === 503) return "ai";
  const title = apiError?.title;
  if (title === "Stream interrupted" || title === "Streaming unavailable" || title === "Stream failed") {
    return "ai";
  }
  return "generic";
}
