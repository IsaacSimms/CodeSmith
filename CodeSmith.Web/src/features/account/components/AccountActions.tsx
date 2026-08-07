// == Account section: sign-out (duplicates nav) + documented closure contact == //
import { useMsal } from "@azure/msal-react";
import { isMsalConfigured } from "../../../auth/msalConfig";

// Documented support path only — no self-serve deletion (map constraint 11)
const ACCOUNT_CLOSURE_MAILTO =
  "mailto:support@codesmith.app?subject=CodeSmith%20account%20closure%20request";

export function AccountActions() {
  return (
    <div className="flex flex-col gap-5">
      {isMsalConfigured() ? <SignOutButton /> : null}
      <p className="text-sm text-gray-400">
        To close your account,{" "}
        <a
          href={ACCOUNT_CLOSURE_MAILTO}
          className="text-accent underline-offset-2 hover:underline"
        >
          contact support
        </a>
        . Account deletion is not self-serve.
      </p>
    </div>
  );
}

// == Same MSAL logout contract as AuthControls (account pages conventionally carry it) == //
function SignOutButton() {
  const { instance, accounts } = useMsal();

  const onSignOut = () => {
    void instance.logoutRedirect({
      account: accounts[0],
      postLogoutRedirectUri: window.location.origin,
    });
  };

  return (
    <div>
      <button
        type="button"
        onClick={onSignOut}
        className="rounded-md border border-gray-600 px-3 py-1.5 text-sm text-gray-200 transition-colors hover:border-gray-400 hover:text-white"
      >
        Sign out
      </button>
    </div>
  );
}
