// == Resizable Split Hook Tests == //
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { renderHook, act } from "@testing-library/react";
import type React from "react";
import { useResizableSplit } from "./useResizableSplit";

// == Helpers == //

function makeRect(width: number, left = 0): DOMRect {
  return {
    x: left,
    y: 0,
    left,
    top: 0,
    right: left + width,
    bottom: 0,
    width,
    height: 0,
    toJSON: () => ({}),
  } as DOMRect;
}

// Attach a fake DOM element to the container ref with a mocked bounding rect.
// Returns the element so tests can tweak its rect on the fly.
function attachContainer(
  ref: React.RefObject<HTMLDivElement | null>,
  width: number,
  left = 0
): HTMLDivElement {
  const div = document.createElement("div");
  div.getBoundingClientRect = vi.fn(() => makeRect(width, left));
  (ref as { current: HTMLDivElement | null }).current = div;
  return div;
}

function startDrag(onMouseDown: (e: React.MouseEvent) => void) {
  act(() => {
    onMouseDown({ preventDefault: vi.fn() } as unknown as React.MouseEvent);
  });
}

function fireMouseMove(clientX: number) {
  act(() => {
    window.dispatchEvent(new MouseEvent("mousemove", { clientX, bubbles: true }));
  });
}

function fireMouseUp() {
  act(() => {
    window.dispatchEvent(new MouseEvent("mouseup", { bubbles: true }));
  });
}

// == Tests == //

describe("useResizableSplit", () => {
  beforeEach(() => {
    document.body.style.userSelect = "";
    document.body.style.cursor = "";
  });

  afterEach(() => {
    document.body.style.userSelect = "";
    document.body.style.cursor = "";
  });

  it("exposes the initial percent as-is", () => {
    const { result } = renderHook(() => useResizableSplit(60));
    expect(result.current.leftPercent).toBe(60);
  });

  it("defaults to 75 when no initial percent is given", () => {
    const { result } = renderHook(() => useResizableSplit());
    expect(result.current.leftPercent).toBe(75);
  });

  it("updates leftPercent proportionally while dragging", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    startDrag(result.current.dividerProps.onMouseDown);
    fireMouseMove(500);

    expect(result.current.leftPercent).toBe(50);
  });

  it("sets body styles on mousedown and restores them on mouseup", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    startDrag(result.current.dividerProps.onMouseDown);
    expect(document.body.style.userSelect).toBe("none");
    expect(document.body.style.cursor).toBe("col-resize");

    fireMouseUp();
    expect(document.body.style.userSelect).toBe("");
    expect(document.body.style.cursor).toBe("");
  });

  it("clamps to the minimum (30%) when dragging far left", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    startDrag(result.current.dividerProps.onMouseDown);
    fireMouseMove(50);                               // 5% raw — below the floor

    expect(result.current.leftPercent).toBe(30);
  });

  it("clamps to the maximum (85%) when dragging far right", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    startDrag(result.current.dividerProps.onMouseDown);
    fireMouseMove(990);                              // 99% raw — above the ceiling

    expect(result.current.leftPercent).toBe(85);
  });

  it("accounts for the container's left offset", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000, 200);

    startDrag(result.current.dividerProps.onMouseDown);
    fireMouseMove(700);                              // (700 - 200) / 1000 = 50%

    expect(result.current.leftPercent).toBe(50);
  });

  it("stops tracking mousemove after mouseup", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    startDrag(result.current.dividerProps.onMouseDown);
    fireMouseMove(500);
    expect(result.current.leftPercent).toBe(50);

    fireMouseUp();
    fireMouseMove(800);                              // should be ignored
    expect(result.current.leftPercent).toBe(50);
  });

  it("ignores mousemove before a drag has started", () => {
    const { result } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    fireMouseMove(500);                              // no mousedown first

    expect(result.current.leftPercent).toBe(75);
  });

  it("restores body styles when unmounted mid-drag", () => {
    const { result, unmount } = renderHook(() => useResizableSplit(75));
    attachContainer(result.current.containerRef, 1000);

    startDrag(result.current.dividerProps.onMouseDown);
    expect(document.body.style.userSelect).toBe("none");

    unmount();
    expect(document.body.style.userSelect).toBe("");
    expect(document.body.style.cursor).toBe("");
  });
});
