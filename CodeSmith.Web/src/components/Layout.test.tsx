// == Layout Tests == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { Layout } from "./Layout";
import { NavigationProvider } from "../contexts/NavigationContext";

function renderLayoutAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <NavigationProvider>
        <Routes>
          <Route element={<Layout />}>
            <Route path="/home" element={<div>home child</div>} />
            <Route path="/other" element={<div>other child</div>} />
          </Route>
        </Routes>
      </NavigationProvider>
    </MemoryRouter>
  );
}

describe("Layout", () => {
  it("renders the CodeSmith logo linking to /home", () => {
    renderLayoutAt("/home");
    const link = screen.getByRole("link", { name: "CodeSmith" });
    expect(link).toHaveAttribute("href", "/home");
  });

  it("renders the child route via Outlet", () => {
    renderLayoutAt("/other");
    expect(screen.getByText("other child")).toBeInTheDocument();
  });
});
