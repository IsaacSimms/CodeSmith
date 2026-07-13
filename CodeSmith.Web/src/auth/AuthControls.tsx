// == Nav auth controls (Sign in / Sign out) == //
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { buildLoginRequest, isMsalConfigured } from "./msalConfig";

export function AuthControls() {
  if (!isMsalConfigured()) return null;
  return <AuthControlsInner />;
}

function AuthControlsInner() {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  const onSignIn = () => {
    void instance.loginRedirect(buildLoginRequest());
  };

  const onSignOut = () => {
    void instance.logoutRedirect({
      account: accounts[0],
      postLogoutRedirectUri: window.location.origin,
    });
  };

  if (isAuthenticated) {
    const label = accounts[0]?.username ?? accounts[0]?.name ?? "Signed in";
    return (
      <div className="ml-auto flex items-center gap-3">
        <span className="hidden max-w-[14rem] truncate text-xs text-gray-400 sm:inline" title={label}>
          {label}
        </span>
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

  return (
    <div className="ml-auto">
      <button
        type="button"
        onClick={onSignIn}
        className="rounded-md bg-accent px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-accent-hover"
      >
        Sign in
      </button>
    </div>
  );
}
