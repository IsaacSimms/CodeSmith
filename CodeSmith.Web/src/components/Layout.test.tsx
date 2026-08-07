// == Layout Tests == //
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { Layout } from "./Layout";
import { NavigationProvider } from "../contexts/NavigationContext";

const isMsalConfigured = vi.fn();

vi.mock("../auth/msalConfig", () => ({
  isMsalConfigured: () => isMsalConfigured(),
}));

// AuthControls uses MSAL hooks; isolate Layout from that surface.
vi.mock("../auth/AuthControls", () => ({
  AuthControls: () => <div data-testid="auth-controls" />,
}));

function renderLayoutAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <NavigationProvider>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/home" element={<div>home child</div>} />
            <Route path="/other" element={<div>other child</div>} />
            <Route path="/account" element={<div>account child</div>} />
          </Route>
        </Routes>
      </NavigationProvider>
    </MemoryRouter>
  );
}

describe("Layout", () => {
  beforeEach(() => {
    isMsalConfigured.mockReturnValue(false);
  });

  it("renders the CodeSmith logo linking to /home", () => {
    renderLayoutAt("/home");
    const link = screen.getByRole("link", { name: "CodeSmith" });
    expect(link).toHaveAttribute("href", "/home");
  });

  it("renders the child route via Outlet", () => {
    renderLayoutAt("/other");
    expect(screen.getByText("other child")).toBeInTheDocument();
  });

  it("shows a nav entry to /account when MSAL is unconfigured (dev reachability)", () => {
    isMsalConfigured.mockReturnValue(false);
    renderLayoutAt("/home");

    const link = screen.getByRole("link", { name: "Account" });
    expect(link).toHaveAttribute("href", "/account");
  });

  it("hides the Account nav entry when MSAL is configured (AuthControls owns entry later)", () => {
    isMsalConfigured.mockReturnValue(true);
    renderLayoutAt("/home");

    expect(screen.queryByRole("link", { name: "Account" })).not.toBeInTheDocument();
  });
});
