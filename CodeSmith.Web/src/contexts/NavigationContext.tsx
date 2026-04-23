// == Navigation Reset Context == //
import { createContext, useCallback, useContext, useRef, type ReactNode } from "react";

interface NavigationContextValue {
  registerReset: (id: string, fn: () => void) => void;
  unregisterReset: (id: string) => void;
  resetAll: () => void;
}

const NavigationContext = createContext<NavigationContextValue | null>(null);

export function NavigationProvider({ children }: { children: ReactNode }) {
  const registry = useRef<Map<string, () => void>>(new Map());

  const registerReset   = useCallback((id: string, fn: () => void) => { registry.current.set(id, fn); }, []);
  const unregisterReset = useCallback((id: string) => { registry.current.delete(id); }, []);
  const resetAll        = useCallback(() => { registry.current.forEach((fn) => fn()); }, []);

  return (
    <NavigationContext.Provider value={{ registerReset, unregisterReset, resetAll }}>
      {children}
    </NavigationContext.Provider>
  );
}

export function useNavigationContext() {
  const ctx = useContext(NavigationContext);
  if (!ctx) throw new Error("useNavigationContext must be used within NavigationProvider");
  return ctx;
}
