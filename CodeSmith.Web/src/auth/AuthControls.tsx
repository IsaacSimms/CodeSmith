// == Nav auth controls (Sign in chooser / authenticated balance dropdown) == //
import {
  useEffect,
  useRef,
  useState,
  type Dispatch,
  type ReactNode,
  type RefObject,
  type SetStateAction,
} from "react";
import { Link } from "react-router-dom";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import type { UseQueryResult } from "@tanstack/react-query";
import { buildGoogleLoginRequest, buildLoginRequest, isMsalConfigured } from "./msalConfig";
import { resolveAccountLabel } from "./resolveAccountLabel";
import { useBalance } from "../features/account/hooks/useBalance";
import { useQuota } from "../features/account/hooks/useQuota";
import {
  formatBalanceUsd,
  formatTokenCount,
  freeTokensRemaining,
} from "../features/account/formatters";
import type { BalanceResponse, QuotaResponse } from "../features/account/types";

export function AuthControls() {
  if (!isMsalConfigured()) return null;
  return <AuthControlsInner />;
}

function AuthControlsInner() {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // == Dismiss chooser / account menu on outside click / Escape == //
  useEffect(() => {
    if (!menuOpen) return;

    const onPointerDown = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMenuOpen(false);
    };

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [menuOpen]);

  const onContinueWithEmail = () => {
    setMenuOpen(false);
    void instance.loginRedirect(buildLoginRequest());
  };

  const onContinueWithGoogle = () => {
    setMenuOpen(false);
    void instance.loginRedirect(buildGoogleLoginRequest());
  };

  const onSignOut = () => {
    void instance.logoutRedirect({
      account: accounts[0],
      postLogoutRedirectUri: window.location.origin,
    });
  };

  if (isAuthenticated) {
    return (
      <AuthenticatedMenu
        label={resolveAccountLabel(accounts[0])}
        menuOpen={menuOpen}
        setMenuOpen={setMenuOpen}
        menuRef={menuRef}
        onSignOut={onSignOut}
      />
    );
  }

  return (
    <div className="relative ml-auto" ref={menuRef}>
      <button
        type="button"
        onClick={() => setMenuOpen((open) => !open)}
        aria-expanded={menuOpen}
        aria-haspopup="menu"
        className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-accent-hover"
      >
        Sign in
      </button>

      {menuOpen && (
        <div
          role="menu"
          className="absolute right-0 z-50 mt-2 w-64 rounded-md border border-gray-600 bg-gray-900 py-1 shadow-lg"
        >
          <button
            type="button"
            role="menuitem"
            onClick={onContinueWithEmail}
            className="block w-full px-3 py-2 text-left text-sm text-gray-100 transition-colors hover:bg-gray-800"
          >
            Continue with email
          </button>
          <button
            type="button"
            role="menuitem"
            onClick={onContinueWithGoogle}
            className="block w-full px-3 py-2 text-left text-sm text-gray-100 transition-colors hover:bg-gray-800"
          >
            Continue with Google
          </button>
          <div className="space-y-1 border-t border-gray-700 px-3 py-2 text-xs text-gray-400">
            <p>Use the same sign-in method next time.</p>
            {/* Google's consent screen labels the Entra federation callback host, not CodeSmith */}
            <p>ciamlogin.com is CodeSmith's Microsoft sign-in host.</p>
          </div>
        </div>
      )}
    </div>
  );
}

// == Authenticated nav dropdown: label toggle + balance summary → Account → Sign out == //
// Hooks stay mounted while authenticated (menu open or closed) so Layout prefetch + turn-settle
// invalidation keep one shared cache warm — never fetch-on-open (ticket 007 #6/#7).
function AuthenticatedMenu({
  label,
  menuOpen,
  setMenuOpen,
  menuRef,
  onSignOut,
}: {
  label: string;
  menuOpen: boolean;
  setMenuOpen: Dispatch<SetStateAction<boolean>>;
  menuRef: RefObject<HTMLDivElement | null>;
  onSignOut: () => void;
}) {
  const quota = useQuota();
  const balance = useBalance();

  return (
    <div className="relative ml-auto" ref={menuRef}>
      <button
        type="button"
        onClick={() => setMenuOpen((open) => !open)}
        aria-expanded={menuOpen}
        aria-haspopup="menu"
        title={label}
        className="max-w-[14rem] truncate rounded-md border border-gray-600 px-3 py-1.5 text-sm text-gray-200 transition-colors hover:border-gray-400 hover:text-white"
      >
        {label}
      </button>

      {menuOpen && (
        <div
          role="menu"
          className="absolute right-0 z-50 mt-2 w-64 rounded-md border border-gray-600 bg-gray-900 py-1 shadow-lg"
        >
          <BalanceSummaryRow quota={quota} balance={balance} />
          <Link
            to="/account"
            role="menuitem"
            onClick={() => setMenuOpen(false)}
            className="block w-full px-3 py-2 text-left text-sm text-gray-100 transition-colors hover:bg-gray-800"
          >
            Account
          </Link>
          <button
            type="button"
            role="menuitem"
            onClick={onSignOut}
            className="block w-full px-3 py-2 text-left text-sm text-gray-100 transition-colors hover:bg-gray-800"
          >
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}

// == Passive balance summary: free remaining while grant has headroom, paid USD after == //
// IP constraint never appears here (ticket 007 #4). Error → omit row; loading → muted slot.
function BalanceSummaryRow({
  quota,
  balance,
}: {
  quota: UseQueryResult<QuotaResponse, Error>;
  balance: UseQueryResult<BalanceResponse, Error>;
}) {
  if (quota.isError) return null;

  // Mode unknown until quota succeeds — stable muted slot, never invent a figure.
  if (quota.isPending || quota.isLoading || !quota.data) {
    return <PassiveSummarySlot muted>—</PassiveSummarySlot>;
  }

  const { freeTokensUsed, freeQuotaMax } = quota.data;
  const freeActive = freeTokensUsed < freeQuotaMax;

  if (freeActive) {
    const remaining = freeTokensRemaining(freeTokensUsed, freeQuotaMax);
    return (
      <PassiveSummarySlot>
        {formatTokenCount(remaining)} free tokens
      </PassiveSummarySlot>
    );
  }

  // Paid mode: balance backs the active figure.
  if (balance.isError) return null;
  if (balance.isPending || balance.isLoading || !balance.data) {
    return <PassiveSummarySlot muted>—</PassiveSummarySlot>;
  }

  return (
    <PassiveSummarySlot>
      {formatBalanceUsd(balance.data.paidCreditsUsd)} credits
    </PassiveSummarySlot>
  );
}

// Passive text — not a menuitem, not a link, not focusable navigation (ticket 007 #5).
function PassiveSummarySlot({
  children,
  muted = false,
}: {
  children: ReactNode;
  muted?: boolean;
}) {
  return (
    <div
      data-testid="balance-summary"
      className={`select-none px-3 py-2 text-sm ${muted ? "text-gray-500" : "text-gray-300"}`}
      aria-live="polite"
    >
      {children}
    </div>
  );
}
