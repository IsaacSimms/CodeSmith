// == Nav auth controls (Sign in chooser / Sign out) == //
import { useEffect, useRef, useState } from "react";
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { buildGoogleLoginRequest, buildLoginRequest, isMsalConfigured } from "./msalConfig";

export function AuthControls() {
  if (!isMsalConfigured()) return null;
  return <AuthControlsInner />;
}

function AuthControlsInner() {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  // == Dismiss chooser on outside click / Escape == //
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
          <p className="border-t border-gray-700 px-3 py-2 text-xs text-gray-400">
            Use the same sign-in method next time.
          </p>
        </div>
      )}
    </div>
  );
}
