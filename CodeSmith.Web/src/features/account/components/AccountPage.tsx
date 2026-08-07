// == Account page shell == //
import type { ReactNode } from "react";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import {
  buildGoogleLoginRequest,
  buildLoginRequest,
  isMsalConfigured,
} from "../../../auth/msalConfig";
import { resolveAccountLabel } from "../../../auth/resolveAccountLabel";
import { AccountSection } from "./AccountSection";

/// Own scroller (Layout main is overflow-hidden). Identity + section slots; data cards fill later.
export function AccountPage() {
  // Dev (no MSAL): page must stay usable without AuthControls. Prod: gate on MSAL session.
  if (!isMsalConfigured()) {
    return <AccountShell identityLabel="Developer" />;
  }
  return <AccountPageWithAuth />;
}

function AccountPageWithAuth() {
  const isAuthenticated = useIsAuthenticated();
  const { instance, accounts } = useMsal();

  if (!isAuthenticated) {
    return (
      <AccountScroller>
        <UnauthenticatedPanel
          onEmail={() => void instance.loginRedirect(buildLoginRequest())}
          onGoogle={() => void instance.loginRedirect(buildGoogleLoginRequest())}
        />
      </AccountScroller>
    );
  }

  return <AccountShell identityLabel={resolveAccountLabel(accounts[0])} />;
}

// == Authenticated (or MSAL-off) layout: identity → banner → wallet → history → prefs → account == //
function AccountShell({ identityLabel }: { identityLabel: string }) {
  return (
    <AccountScroller>
      <header data-testid="account-identity-header" className="mb-8">
        <p className="mb-1 text-sm text-gray-400">Account</p>
        <h1 className="truncate text-2xl font-semibold text-white">{identityLabel}</h1>
      </header>

      {/* Post-checkout banner slot (work 013); reserves no height when empty */}
      <div data-testid="account-banner-slot" />

      {/* Wallet row: credits + free quota side by side (work 010 / 011 fill bodies) */}
      <div
        data-testid="account-wallet-row"
        className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2"
      >
        <AccountSection title="Credits" anchorId="credits" isLoading={false} error={null}>
          {null}
        </AccountSection>
        <AccountSection title="Free tokens" anchorId="free-quota" isLoading={false} error={null}>
          {null}
        </AccountSection>
      </div>

      <div className="flex flex-col gap-6">
        <AccountSection title="History" anchorId="history" isLoading={false} error={null}>
          {null}
        </AccountSection>
        <AccountSection title="Preferences" anchorId="preferences" isLoading={false} error={null}>
          {null}
        </AccountSection>
        <AccountSection title="Account" anchorId="account" isLoading={false} error={null}>
          {null}
        </AccountSection>
      </div>
    </AccountScroller>
  );
}

function AccountScroller({ children }: { children: ReactNode }) {
  return (
    <div
      data-testid="account-page-scroller"
      className="h-full overflow-y-auto"
    >
      <div className="mx-auto w-full max-w-4xl px-6 py-8">{children}</div>
    </div>
  );
}

// == Single sign-in panel — never six cards each asking for login == //
function UnauthenticatedPanel({
  onEmail,
  onGoogle,
}: {
  onEmail: () => void;
  onGoogle: () => void;
}) {
  return (
    <div className="mx-auto max-w-md rounded-xl border border-gray-700 bg-gray-900 px-8 py-10 text-center">
      <h1 className="mb-3 text-xl font-semibold text-white">Sign in to view your account</h1>
      <p className="mb-6 text-sm text-gray-400">
        Credits, free quota, and transaction history require a signed-in account.
      </p>
      <div className="flex flex-col gap-2">
        <button
          type="button"
          onClick={onEmail}
          className="rounded-md bg-accent px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-accent-hover"
        >
          Continue with email
        </button>
        <button
          type="button"
          onClick={onGoogle}
          className="rounded-md border border-gray-600 px-4 py-2 text-sm text-gray-200 transition-colors hover:border-gray-400 hover:text-white"
        >
          Continue with Google
        </button>
      </div>
    </div>
  );
}
