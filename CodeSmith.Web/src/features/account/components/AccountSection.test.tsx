// == AccountSection wrapper == //
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, act } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import type { ClientFailure } from "../../../lib/clientError";
import { AccountSection, type AccountSectionProps } from "./AccountSection";

const sampleError: ClientFailure = {
  kind: "generic",
  title: "Something went wrong",
  detail: "Please try again.",
};

function renderSection(
  props: Partial<AccountSectionProps> = {},
  initialEntry = "/account"
) {
  const defaults = {
    title: "Credits",
    anchorId: "credits",
    isLoading: false,
    error: null as ClientFailure | null,
    children: <p>Balance body</p>,
  };
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <AccountSection {...defaults} {...props} />
    </MemoryRouter>
  );
}

describe("AccountSection", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("renders card chrome with the section title", () => {
    renderSection();
    expect(screen.getByRole("heading", { name: "Credits" })).toBeInTheDocument();
    const section = screen.getByTestId("account-section-credits");
    expect(section).toHaveClass("rounded-xl", "border", "border-gray-700", "bg-gray-900");
  });

  it("shows a muted loading line while loading", () => {
    renderSection({ isLoading: true, children: <p>Balance body</p> });
    expect(screen.getByText("Loading…")).toBeInTheDocument();
    expect(screen.queryByText("Balance body")).not.toBeInTheDocument();
  });

  it("shows FailureNotice when error is set and not loading", () => {
    renderSection({ error: sampleError, children: <p>Balance body</p> });
    expect(screen.getByTestId("failure-notice")).toBeInTheDocument();
    expect(screen.getByText("Something went wrong")).toBeInTheDocument();
    expect(screen.queryByText("Balance body")).not.toBeInTheDocument();
  });

  it("renders children when loaded with no error", () => {
    renderSection({ children: <p>Balance body</p> });
    expect(screen.getByText("Balance body")).toBeInTheDocument();
    expect(screen.queryByText("Loading…")).not.toBeInTheDocument();
  });

  it("keeps stable body min-height between loading and loaded", () => {
    const { rerender } = renderSection({ isLoading: true, children: <p>Balance body</p> });
    const bodyWhileLoading = screen.getByTestId("account-section-body");
    const loadingHeight = bodyWhileLoading.getBoundingClientRect().height;
    expect(bodyWhileLoading).toHaveClass("min-h-24");

    rerender(
      <MemoryRouter initialEntries={["/account"]}>
        <AccountSection
          title="Credits"
          anchorId="credits"
          isLoading={false}
          error={null}
        >
          <p>Balance body</p>
        </AccountSection>
      </MemoryRouter>
    );

    const bodyWhenLoaded = screen.getByTestId("account-section-body");
    expect(bodyWhenLoaded).toHaveClass("min-h-24");
    expect(bodyWhenLoaded.getBoundingClientRect().height).toBe(loadingHeight);
  });

  it("scrolls into view and applies a transient ring when location.hash matches anchorId", () => {
    const scrollSpy = vi.spyOn(Element.prototype, "scrollIntoView");

    renderSection({}, "/account#credits");

    const section = screen.getByTestId("account-section-credits");
    expect(scrollSpy).toHaveBeenCalledWith({ block: "start" });
    expect(section).toHaveAttribute("data-hash-highlight", "true");

    act(() => {
      vi.advanceTimersByTime(2000);
    });
    expect(section).not.toHaveAttribute("data-hash-highlight", "true");

    scrollSpy.mockRestore();
  });

  it("does nothing when location.hash does not match anchorId", () => {
    const scrollSpy = vi.spyOn(Element.prototype, "scrollIntoView");

    renderSection({}, "/account#history");

    const section = screen.getByTestId("account-section-credits");
    expect(scrollSpy).not.toHaveBeenCalled();
    expect(section).not.toHaveAttribute("data-hash-highlight", "true");

    scrollSpy.mockRestore();
  });
});
