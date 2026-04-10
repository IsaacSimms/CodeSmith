// == Resizable Split Hook == //
import { useCallback, useEffect, useRef, useState } from "react";

// Minimum % width for the left panel so Monaco stays usable.
const MIN_LEFT_PERCENT = 30;
// Maximum % width for the left panel so the right panel keeps room for its input + content.
const MAX_LEFT_PERCENT = 85;

interface DividerProps {
  onMouseDown: (event: React.MouseEvent) => void;
}

interface UseResizableSplitResult {
  leftPercent: number;
  dividerProps: DividerProps;
  containerRef: React.RefObject<HTMLDivElement | null>;
}

/// <summary>
/// Hook that powers a horizontally-draggable splitter between two panels.
/// Consumers attach `containerRef` to the flex row that contains both panels,
/// spread `dividerProps` onto a thin divider element between them, and bind
/// `leftPercent` / `100 - leftPercent` to the panel widths. Drag logic uses
/// window-level listeners so the drag survives the cursor leaving the hit area.
/// Touch support is intentionally deferred — add `onTouchStart` + `touchmove`
/// listeners mirroring the mouse handlers if needed.
/// </summary>
export function useResizableSplit(initialPercent = 75): UseResizableSplitResult {
  const [leftPercent, setLeftPercent] = useState(initialPercent);
  const [isDragging, setIsDragging] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);

  // == Begin drag on mousedown == //
  const handleMouseDown = useCallback((event: React.MouseEvent) => {
    event.preventDefault();                              // suppress text-selection start
    setIsDragging(true);
    document.body.style.userSelect = "none";             // block text selection during drag
    document.body.style.cursor = "col-resize";           // keep resize cursor even if pointer drifts off the bar
  }, []);

  // == Track drag on window-level events == //
  useEffect(() => {
    if (!isDragging) return;

    function handleMouseMove(event: MouseEvent) {
      const container = containerRef.current;
      if (!container) return;

      const rect = container.getBoundingClientRect();
      if (rect.width === 0) return;

      const rawPercent = ((event.clientX - rect.left) / rect.width) * 100;
      const clamped = Math.min(MAX_LEFT_PERCENT, Math.max(MIN_LEFT_PERCENT, rawPercent));
      setLeftPercent(clamped);
    }

    function handleMouseUp() {
      setIsDragging(false);
    }

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);

    // Cleanup restores body styles even if the component unmounts mid-drag.
    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
      document.body.style.userSelect = "";
      document.body.style.cursor = "";
    };
  }, [isDragging]);

  return {
    leftPercent,
    dividerProps: { onMouseDown: handleMouseDown },
    containerRef,
  };
}
