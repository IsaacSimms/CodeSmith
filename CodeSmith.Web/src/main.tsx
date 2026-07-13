// == Application Entry Point == //
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { MsalProvider } from "@azure/msal-react";
import App from "./App";
import { initializeMsal } from "./auth/msalInstance";
import "./index.css";

async function bootstrap() {
  const msalInstance = await initializeMsal();
  const root = createRoot(document.getElementById("root")!);

  root.render(
    <StrictMode>
      {msalInstance ? (
        <MsalProvider instance={msalInstance}>
          <App />
        </MsalProvider>
      ) : (
        <App />
      )}
    </StrictMode>
  );
}

void bootstrap();
