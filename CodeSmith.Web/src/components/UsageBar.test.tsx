// == UsageBar primitive: fill math only == //
import { describe, it, expect } from "vitest";
import { render } from "@testing-library/react";
import { UsageBar } from "./UsageBar";

describe("UsageBar", () => {
  it("sets fill width proportional to used / max", () => {
    render(<UsageBar used={100_000} max={200_000} fillClassName="bg-emerald-500" />);
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.style.width).toBe("50%");
  });

  it("clamps fill width to 100% when used exceeds max", () => {
    render(<UsageBar used={300_000} max={200_000} fillClassName="bg-red-500" />);
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.style.width).toBe("100%");
  });

  it("applies a 0.3% minimum visual fill so the bar is never invisible", () => {
    render(<UsageBar used={0} max={200_000} fillClassName="bg-accent" />);
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.style.width).toBe("0.3%");
  });

  it("applies the caller-supplied fill class (no built-in color ramp)", () => {
    render(<UsageBar used={50} max={100} fillClassName="bg-accent" />);
    const fill = document.querySelector("[style]") as HTMLElement;
    expect(fill.className).toContain("bg-accent");
    expect(fill.className).not.toContain("bg-red-500");
    expect(fill.className).not.toContain("bg-emerald-500");
  });
});
