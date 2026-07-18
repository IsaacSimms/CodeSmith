// == Streaming Text Accumulator Hook == //
import { useCallback, useRef, useState } from "react";

/// Accumulates stream deltas into renderable state. The ref mirror exists because mutation
/// onError callbacks close over stale state — getText() always returns the text streamed so far,
/// which is how a failed turn's partial reply gets snapshotted for the error UI.
export function useStreamingText() {
  const [text, setText] = useState("");
  const textRef = useRef("");

  const append = useCallback((delta: string) => {
    textRef.current += delta;
    setText(textRef.current);
  }, []);

  const reset = useCallback(() => {
    textRef.current = "";
    setText("");
  }, []);

  const getText = useCallback(() => textRef.current, []);

  return { text, append, reset, getText };
}
