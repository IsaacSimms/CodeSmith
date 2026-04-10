// == Home Page Tests == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { HomePage } from "./HomePage";

function renderHomePage() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>
  );
}

describe("HomePage", () => {
  it("renders the hero heading", () => {
    renderHomePage();
    expect(
      screen.getByRole("heading", { name: /practice\. learn\. level up\./i })
    ).toBeInTheDocument();
  });

  it("renders a CTA link pointing to /pairedprogrammer", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /start paired programming/i });
    expect(link).toHaveAttribute("href", "/pairedprogrammer");
  });
});
