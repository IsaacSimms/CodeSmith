// == AuthControls sign-in chooser == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AuthControls } from "./AuthControls";

const loginRedirect = vi.fn();
const logoutRedirect = vi.fn();
const useIsAuthenticated = vi.fn();
const useMsal = vi.fn();

vi.mock("@azure/msal-react", () => ({
  useIsAuthenticated: () => useIsAuthenticated(),
  useMsal: () => useMsal(),
}));

vi.mock("./msalConfig", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./msalConfig")>();
  return {
    ...actual,
    isMsalConfigured: () => true,
    buildLoginRequest: () => ({ scopes: ["api://test/access"] }),
    buildGoogleLoginRequest: () => ({
      scopes: ["api://test/access"],
      extraQueryParameters: { domain_hint: "Google" },
    }),
  };
});

beforeEach(() => {
  loginRedirect.mockReset();
  logoutRedirect.mockReset();
  useIsAuthenticated.mockReturnValue(false);
  useMsal.mockReturnValue({
    instance: { loginRedirect, logoutRedirect },
    accounts: [],
  });
});

describe("AuthControls", () => {
  it("shows Sign in when signed out and hides provider options until opened", () => {
    render(<AuthControls />);

    expect(screen.getByRole("button", { name: "Sign in" })).toBeInTheDocument();
    expect(screen.queryByRole("menuitem", { name: "Continue with email" })).not.toBeInTheDocument();
    expect(screen.queryByRole("menuitem", { name: "Continue with Google" })).not.toBeInTheDocument();
  });

  it("opens dropdown with email, Google, and helper text", async () => {
    const user = userEvent.setup();
    render(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByRole("menuitem", { name: "Continue with email" })).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: "Continue with Google" })).toBeInTheDocument();
    expect(screen.getByText("Use the same sign-in method next time.")).toBeInTheDocument();
    expect(
      screen.getByText("ciamlogin.com is CodeSmith's Microsoft sign-in host.")
    ).toBeInTheDocument();
  });

  it("Continue with email calls loginRedirect without domain_hint", async () => {
    const user = userEvent.setup();
    render(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await user.click(screen.getByRole("menuitem", { name: "Continue with email" }));

    expect(loginRedirect).toHaveBeenCalledTimes(1);
    expect(loginRedirect).toHaveBeenCalledWith({ scopes: ["api://test/access"] });
    const firstArgs = loginRedirect.mock.calls[0]?.[0];
    expect(firstArgs).toBeDefined();
    expect(firstArgs).not.toHaveProperty("extraQueryParameters");
  });

  it("Continue with Google calls loginRedirect with domain_hint Google", async () => {
    const user = userEvent.setup();
    render(<AuthControls />);

    await user.click(screen.getByRole("button", { name: "Sign in" }));
    await user.click(screen.getByRole("menuitem", { name: "Continue with Google" }));

    expect(loginRedirect).toHaveBeenCalledTimes(1);
    expect(loginRedirect).toHaveBeenCalledWith({
      scopes: ["api://test/access"],
      extraQueryParameters: { domain_hint: "Google" },
    });
  });

  it("signed in shows Sign out and not the Sign in chooser", async () => {
    useIsAuthenticated.mockReturnValue(true);
    useMsal.mockReturnValue({
      instance: { loginRedirect, logoutRedirect },
      accounts: [{ username: "user@example.com", name: "User" }],
    });

    render(<AuthControls />);

    expect(screen.getByRole("button", { name: "Sign out" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Sign in" })).not.toBeInTheDocument();
    expect(screen.getByText("user@example.com")).toBeInTheDocument();
  });

  it("signed in with a federated account shows the email claim, not the GUID UPN", () => {
    useIsAuthenticated.mockReturnValue(true);
    useMsal.mockReturnValue({
      instance: { loginRedirect, logoutRedirect },
      accounts: [
        {
          username: "be36f73c-1993-4e1c-8064-8a4156082144@codesmithapp.onmicrosoft.com",
          name: "IsaacTestGoogleAuth",
          idTokenClaims: { email: "isaacsimms11@gmail.com" },
        },
      ],
    });

    render(<AuthControls />);

    expect(screen.getByText("isaacsimms11@gmail.com")).toBeInTheDocument();
    expect(screen.queryByText(/be36f73c/)).not.toBeInTheDocument();
  });

  it("Sign out calls logoutRedirect", async () => {
    const user = userEvent.setup();
    const account = { username: "user@example.com", name: "User" };
    useIsAuthenticated.mockReturnValue(true);
    useMsal.mockReturnValue({
      instance: { loginRedirect, logoutRedirect },
      accounts: [account],
    });

    render(<AuthControls />);
    await user.click(screen.getByRole("button", { name: "Sign out" }));

    expect(logoutRedirect).toHaveBeenCalledWith({
      account,
      postLogoutRedirectUri: window.location.origin,
    });
  });
});
