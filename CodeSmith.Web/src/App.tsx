// == App Root Component == //
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { NavigationProvider } from "./contexts/NavigationContext";
import { Layout } from "./components/Layout";
import { HomePage } from "./features/home/components/HomePage";
import { ChatWindow } from "./features/chat/components/ChatWindow";
import { PromptLabWindow } from "./features/prompt-lab/components/PromptLabWindow";
import { SystemLabWindow } from "./features/system-lab/components/SystemLabWindow";
import { BillingResultPage } from "./features/billing/components/BillingResultPage";
import { AccountPage } from "./features/account/components/AccountPage";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false },
  },
});

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <NavigationProvider>
          <Routes>
            <Route element={<Layout />}>
              <Route path="/" element={<Navigate to="/home" replace />} />
              <Route path="/home" element={<HomePage />} />
              <Route path="/pairedprogrammer" element={<ChatWindow />} />
              <Route path="/prompt-lab" element={<PromptLabWindow />} />
              <Route path="/system-lab" element={<SystemLabWindow />} />
              <Route path="/account" element={<AccountPage />} />
              <Route path="/billing/success" element={<BillingResultPage kind="success" />} />
              <Route path="/billing/cancel" element={<BillingResultPage kind="cancel" />} />
            </Route>
          </Routes>
        </NavigationProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}
