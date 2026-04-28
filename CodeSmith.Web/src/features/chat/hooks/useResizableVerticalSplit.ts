// == Resizable Vertical Split Hook == //
import { useCallback, useEffect, useRef, useState } from "react";

// Minimum % height for the top panel (editor) so it stays usable.
const MIN_TOP_PERCENT = 25;
// Maximum % height for the top panel (terminal fully collapsed).
const MAX_TOP_PERCENT = 100;
// Default split: editor gets 75%, terminal gets 25%.
const DEFAULT_TOP_PERCENT = 75;

interface DividerProps {
  onMouseDown: (event: React.MouseEvent) => void;
}

interface UseResizableVerticalSplitResult {
  topPercent: number;
  setTopPercent: (value: number) => void;
  dividerProps: DividerProps;
  containerRef: React.RefObject<HTMLDivElement | null>;
}

// == Hook == //
export function useResizableVerticalSplit(
  initialPercent = DEFAULT_TOP_PERCENT,
  minPercent = MIN_TOP_PERCENT,
  maxPercent = MAX_TOP_PERCENT,
): UseResizableVerticalSplitResult {
  const [topPercent, setTopPercent] = useState(initialPercent);
  const [isDragging, setIsDragging] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);

  // == Begin drag on mousedown == //
  const handleMouseDown = useCallback((event: React.MouseEvent) => {
    event.preventDefault();
    setIsDragging(true);
    document.body.style.userSelect = "none";
    document.body.style.cursor = "row-resize";
  }, []);

  // == Track drag on window-level events == //
  useEffect(() => {
    if (!isDragging) return;

    function handleMouseMove(event: MouseEvent) {
      const container = containerRef.current;
      if (!container) return;

      const rect = container.getBoundingClientRect();
      if (rect.height === 0) return;

      const rawPercent = ((event.clientY - rect.top) / rect.height) * 100;
      const clamped = Math.min(maxPercent, Math.max(minPercent, rawPercent));
      setTopPercent(clamped);
    }

    function handleMouseUp() {
      setIsDragging(false);
    }

    window.addEventListener("mousemove", handleMouseMove);
    window.addEventListener("mouseup", handleMouseUp);

    return () => {
      window.removeEventListener("mousemove", handleMouseMove);
      window.removeEventListener("mouseup", handleMouseUp);
      document.body.style.userSelect = "";
      document.body.style.cursor = "";
    };
  }, [isDragging, minPercent, maxPercent]);

  return {
    topPercent,
    setTopPercent,
    dividerProps: { onMouseDown: handleMouseDown },
    containerRef,
  };
}
