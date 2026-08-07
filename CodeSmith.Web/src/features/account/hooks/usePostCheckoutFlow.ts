// == Post-checkout: URL intent, ledger poll, Account banner == //
// Completion = new TopUp vs checkout-time baseline — never balance delta (ticket 006).
import { useCallback, useEffect, useRef, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { getLedger } from "../../../lib/apiClient";
import {
  clearCheckoutBaseline,
  findNewTopUp,
  isCheckoutPending,
  readCheckoutBaseline,
} from "../checkoutBaseline";
import { accountQueryKeys } from "../queryKeys";
import { invalidateAccountUsageQueries } from "./invalidateAccountUsageQueries";

export const POST_CHECKOUT_POLL_MS = 2_000;
export const POST_CHECKOUT_TIMEOUT_MS = 30_000;
export const CREDITS_ADDED_DISMISS_MS = 4_000;

export const POST_CHECKOUT_COPY = {
  applying: "Applying your credits…",
  creditsAdded: "Credits added",
  giveUp:
    "Payment received — credits may take a moment to appear. Refresh this page if the balance doesn't update.",
  canceled: "Checkout canceled — no charge was made.",
} as const;

export type PostCheckoutBannerKind =
  | "applying"
  | "creditsAdded"
  | "giveUp"
  | "canceled";

export interface PostCheckoutBanner {
  kind: PostCheckoutBannerKind;
  message: string;
}

/// Owns `?checkout=` handling, sessionStorage baseline, and ledger polling for TopUp.
export function usePostCheckoutFlow() {
  const queryClient = useQueryClient();
  const [searchParams, setSearchParams] = useSearchParams();
  const [banner, setBanner] = useState<PostCheckoutBanner | null>(null);

  const pollGeneration = useRef(0);
  const timeoutId = useRef<ReturnType<typeof setTimeout> | null>(null);
  const autoDismissId = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Resume-on-mount only once so effect re-runs after strip do not restart blindly
  const resumeChecked = useRef(false);

  const clearTimers = useCallback(() => {
    if (timeoutId.current !== null) {
      clearTimeout(timeoutId.current);
      timeoutId.current = null;
    }
    if (autoDismissId.current !== null) {
      clearTimeout(autoDismissId.current);
      autoDismissId.current = null;
    }
  }, []);

  const stopPoll = useCallback(() => {
    pollGeneration.current += 1;
    if (timeoutId.current !== null) {
      clearTimeout(timeoutId.current);
      timeoutId.current = null;
    }
  }, []);

  const showGiveUp = useCallback(() => {
    stopPoll();
    setBanner({ kind: "giveUp", message: POST_CHECKOUT_COPY.giveUp });
  }, [stopPoll]);

  const showCreditsAdded = useCallback(() => {
    stopPoll();
    clearCheckoutBaseline();
    invalidateAccountUsageQueries(queryClient);
    setBanner({ kind: "creditsAdded", message: POST_CHECKOUT_COPY.creditsAdded });
    autoDismissId.current = setTimeout(() => {
      setBanner((current) => (current?.kind === "creditsAdded" ? null : current));
      autoDismissId.current = null;
    }, CREDITS_ADDED_DISMISS_MS);
  }, [queryClient, stopPoll]);

  // == Poll ledger: immediate, then every 2s, until new TopUp or 30s == //
  const startPoll = useCallback(() => {
    stopPoll();
    const generation = pollGeneration.current;
    const startedAt = Date.now();
    setBanner({ kind: "applying", message: POST_CHECKOUT_COPY.applying });

    const scheduleNext = (delayMs: number) => {
      timeoutId.current = setTimeout(() => {
        void tick();
      }, delayMs);
    };

    const tick = async () => {
      if (generation !== pollGeneration.current) return;

      const baseline = readCheckoutBaseline();
      if (!baseline) {
        showGiveUp();
        return;
      }

      try {
        const entries = await getLedger();
        if (generation !== pollGeneration.current) return;

        // Keep the history section warm while we watch for the webhook
        queryClient.setQueryData([...accountQueryKeys.ledger, 20], entries);

        if (findNewTopUp(entries, baseline.fingerprints)) {
          showCreditsAdded();
          return;
        }
      } catch {
        // Transient read failures: keep polling until the deadline
      }

      if (generation !== pollGeneration.current) return;

      const elapsed = Date.now() - startedAt;
      if (elapsed >= POST_CHECKOUT_TIMEOUT_MS) {
        showGiveUp();
        return;
      }

      scheduleNext(POST_CHECKOUT_POLL_MS);
    };

    void tick();
  }, [queryClient, showCreditsAdded, showGiveUp, stopPoll]);

  // == Accept Stripe return query once, then key off pending == //
  useEffect(() => {
    const checkout = searchParams.get("checkout");

    if (checkout === "success") {
      resumeChecked.current = true;
      // Strip first so a refresh cannot re-accept from the query string
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete("checkout");
          return next;
        },
        { replace: true }
      );

      if (!readCheckoutBaseline()) {
        // Missing baseline → give-up; never invent success from a time window
        setBanner({ kind: "giveUp", message: POST_CHECKOUT_COPY.giveUp });
        return;
      }

      startPoll();
      return;
    }

    if (checkout === "cancel") {
      resumeChecked.current = true;
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete("checkout");
          return next;
        },
        { replace: true }
      );
      clearCheckoutBaseline();
      stopPoll();
      setBanner({ kind: "canceled", message: POST_CHECKOUT_COPY.canceled });
      return;
    }

    // No query: resume an in-flight pending poll after a mid-flow refresh
    if (!resumeChecked.current) {
      resumeChecked.current = true;
      if (isCheckoutPending()) {
        startPoll();
      }
    }
  }, [searchParams, setSearchParams, startPoll, stopPoll]);

  useEffect(() => () => {
    stopPoll();
    clearTimers();
  }, [clearTimers, stopPoll]);

  const dismissBanner = useCallback(() => {
    if (banner?.kind === "giveUp" || banner?.kind === "canceled") {
      clearCheckoutBaseline();
    }
    setBanner(null);
  }, [banner]);

  return { banner, dismissBanner };
}
