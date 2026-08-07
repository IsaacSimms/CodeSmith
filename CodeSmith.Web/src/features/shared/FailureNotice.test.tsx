// == FailureNotice == //
import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { ClientFailure } from "../../lib/clientError";
import { FailureNotice } from "./FailureNotice";

const generic: ClientFailure = {
  kind: "generic",
  title: "Something went wrong",
  detail: "An unexpected error occurred. Please try again.",
};

const paywall: ClientFailure = {
  kind: "paywall",
  title: "Out of free quota and credits",
  detail: "You don't have enough remaining free usage or purchased credits for this request.",
  action: { label: "Add credits", href: "/account#credits" },
};

function renderNotice(failure: ClientFailure, className?: string) {
  return render(
    <MemoryRouter>
      <FailureNotice failure={failure} className={className} />
    </MemoryRouter>
  );
}

describe("FailureNotice", () => {
  it("renders a CTA link when failure.action is present", () => {
    renderNotice(paywall);

    const link = screen.getByRole("link", { name: "Add credits" });
    expect(link).toHaveAttribute("href", "/account#credits");
    expect(screen.getByText("Out of free quota and credits")).toBeInTheDocument();
    expect(screen.getByText(/don't have enough remaining free usage/i)).toBeInTheDocument();
  });

  it("renders title and detail only when failure has no action", () => {
    renderNotice(generic);

    expect(screen.getByTestId("failure-notice")).toBeInTheDocument();
    expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    expect(screen.getByText("An unexpected error occurred. Please try again.")).toBeInTheDocument();
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
  });
});
