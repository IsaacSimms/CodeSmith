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
      screen.getByRole("heading", { name: /forge the skill\. then prove it\./i })
    ).toBeInTheDocument();
  });

  it("renders the hero subtext", () => {
    renderHomePage();
    expect(
      screen.getByText(/multiple disciplines\. one habit: deliberate practice\./i)
    ).toBeInTheDocument();
  });


  it("renders a CTA link pointing to /pairedprogrammer", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /paired programmer/i });
    expect(link).toHaveAttribute("href", "/pairedprogrammer");
  });

  it("renders a CTA link pointing to /prompt-lab", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /prompt lab/i });
    expect(link).toHaveAttribute("href", "/prompt-lab");
  });

  it("renders a CTA link pointing to /system-lab", () => {
    renderHomePage();
    const link = screen.getByRole("link", { name: /system lab/i });
    expect(link).toHaveAttribute("href", "/system-lab");
  });

  it("lists Paired Programmer before the secondary lab CTAs in the document", () => {
    renderHomePage();
    const links = screen
      .getAllByRole("link")
      .filter((el) =>
        ["/pairedprogrammer", "/prompt-lab", "/system-lab"].includes(
          el.getAttribute("href") ?? ""
        )
      );
    expect(links.map((el) => el.getAttribute("href"))).toEqual([
      "/pairedprogrammer",
      "/prompt-lab",
      "/system-lab",
    ]);
  });

  it("renders the solo-maintainer scale-to-zero note at the bottom", () => {
    renderHomePage();
    expect(
      screen.getByText(/CodeSmith is developed and maintained by one engineer\. \(Hello!\)/i)
    ).toBeInTheDocument();
    expect(screen.getByText(/scale to zero wherever possible/i)).toBeInTheDocument();
    expect(screen.getByText(/give the servers a few seconds to spin up/i)).toBeInTheDocument();
    expect(screen.getByText(/enjoy the craft of engineering/i)).toBeInTheDocument();
    expect(screen.getByText(/With love, Isaac/i)).toBeInTheDocument();
  });
});
