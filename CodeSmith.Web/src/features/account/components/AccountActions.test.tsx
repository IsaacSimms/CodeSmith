// == Account section actions (sign-out + closure contact) == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AccountActions } from "./AccountActions";

const logoutRedirect = vi.fn();
const useMsal = vi.fn();
const isMsalConfigured = vi.fn();

vi.mock("@azure/msal-react", () => ({
  useMsal: () => useMsal(),
}));

vi.mock("../../../auth/msalConfig", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../auth/msalConfig")>();
  return {
    ...actual,
    isMsalConfigured: () => isMsalConfigured(),
  };
});

beforeEach(() => {
  logoutRedirect.mockReset();
  isMsalConfigured.mockReturnValue(true);
  useMsal.mockReturnValue({
    instance: { logoutRedirect },
    accounts: [{ username: "user@example.com", name: "User" }],
  });
});

describe("AccountActions", () => {
  it("Sign out calls logoutRedirect with the same args as the nav dropdown", async () => {
    const user = userEvent.setup();
    const account = { username: "user@example.com", name: "User" };
    useMsal.mockReturnValue({
      instance: { logoutRedirect },
      accounts: [account],
    });

    render(<AccountActions />);
    await user.click(screen.getByRole("button", { name: "Sign out" }));

    expect(logoutRedirect).toHaveBeenCalledWith({
      account,
      postLogoutRedirectUri: window.location.origin,
    });
  });

  it("documents the account-closure support-contact path without self-serve deletion", () => {
    render(<AccountActions />);

    expect(screen.getByText(/close your account/i)).toBeInTheDocument();
    expect(screen.getByText(/not self-serve/i)).toBeInTheDocument();
    const link = screen.getByRole("link", { name: /contact support/i });
    expect(link).toHaveAttribute("href", expect.stringMatching(/^mailto:/));
  });

  it("omits Sign out when MSAL is unconfigured (matches AuthControls)", () => {
    isMsalConfigured.mockReturnValue(false);

    render(<AccountActions />);

    expect(screen.queryByRole("button", { name: "Sign out" })).not.toBeInTheDocument();
    // Closure contact still present
    expect(screen.getByRole("link", { name: /contact support/i })).toBeInTheDocument();
  });
});
