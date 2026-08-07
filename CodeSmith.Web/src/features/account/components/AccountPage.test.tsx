// == Account page shell == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { AccountPage } from "./AccountPage";

const loginRedirect = vi.fn();
const useIsAuthenticated = vi.fn();
const useMsal = vi.fn();
const isMsalConfigured = vi.fn();

vi.mock("@azure/msal-react", () => ({
  useIsAuthenticated: () => useIsAuthenticated(),
  useMsal: () => useMsal(),
}));

vi.mock("../../../auth/msalConfig", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../auth/msalConfig")>();
  return {
    ...actual,
    isMsalConfigured: () => isMsalConfigured(),
    buildLoginRequest: () => ({ scopes: ["api://test/access"] }),
    buildGoogleLoginRequest: () => ({
      scopes: ["api://test/access"],
      extraQueryParameters: { domain_hint: "Google" },
    }),
  };
});

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/account" element={<AccountPage />} />
        <Route path="/home" element={<div>home</div>} />
      </Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  loginRedirect.mockReset();
  isMsalConfigured.mockReturnValue(true);
  useIsAuthenticated.mockReturnValue(true);
  useMsal.mockReturnValue({
    instance: { loginRedirect },
    accounts: [{ username: "user@example.com", name: "User" }],
  });
});

describe("AccountPage", () => {
  it("renders an identity header from resolveAccountLabel inside its own scroller", () => {
    renderAt("/account");

    const header = screen.getByTestId("account-identity-header");
    expect(header).toContainElement(
      screen.getByRole("heading", { level: 1, name: "user@example.com" })
    );
    expect(header).toHaveTextContent("Account");

    const scroller = screen.getByTestId("account-page-scroller");
    expect(scroller).toHaveClass("h-full", "overflow-y-auto");
    expect(scroller).toContainElement(header);
  });

  it("renders the structural slots: banner, wallet row, history, preferences, account", () => {
    renderAt("/account");

    expect(screen.getByTestId("account-banner-slot")).toBeInTheDocument();
    expect(screen.getByTestId("account-wallet-row")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-credits")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-history")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-preferences")).toBeInTheDocument();
    expect(screen.getByTestId("account-section-account")).toBeInTheDocument();
  });

  it("renders a single sign-in panel when unauthenticated and does not redirect", () => {
    useIsAuthenticated.mockReturnValue(false);

    renderAt("/account");

    expect(screen.getByRole("heading", { name: /sign in to view your account/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /continue with email/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /continue with google/i })).toBeInTheDocument();
    // Shell chrome still present: scroller, not a navigate-away
    expect(screen.getByTestId("account-page-scroller")).toBeInTheDocument();
    // No multi-card "sign in" noise
    expect(screen.queryByTestId("account-section-credits")).not.toBeInTheDocument();
    expect(screen.queryByText("home")).not.toBeInTheDocument();
  });

  it("sign-in panel email button calls loginRedirect", async () => {
    useIsAuthenticated.mockReturnValue(false);
    const user = userEvent.setup();

    renderAt("/account");
    await user.click(screen.getByRole("button", { name: /continue with email/i }));

    expect(loginRedirect).toHaveBeenCalledWith({ scopes: ["api://test/access"] });
  });

  it("when MSAL is unconfigured, renders the authenticated shell without a sign-in gate", () => {
    isMsalConfigured.mockReturnValue(false);

    renderAt("/account");

    expect(screen.queryByRole("heading", { name: /sign in to view your account/i })).not.toBeInTheDocument();
    expect(screen.getByTestId("account-section-credits")).toBeInTheDocument();
    expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
  });
});
