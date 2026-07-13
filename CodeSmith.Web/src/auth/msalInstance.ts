// == MSAL PublicClientApplication bootstrap == //
import { PublicClientApplication, type IPublicClientApplication } from "@azure/msal-browser";
import { buildLoginRequest, buildMsalConfig, getApiScope, isMsalConfigured } from "./msalConfig";
import { setAccessTokenProvider } from "../lib/apiClient";

let msalInstance: IPublicClientApplication | null = null;

export function getMsalInstance(): IPublicClientApplication | null {
  return msalInstance;
}

// == Create instance, handle redirect, wire apiClient token provider == //
export async function initializeMsal(): Promise<IPublicClientApplication | null> {
  if (!isMsalConfigured()) {
    setAccessTokenProvider(null);
    return null;
  }

  const instance = new PublicClientApplication(buildMsalConfig());
  await instance.initialize();
  await instance.handleRedirectPromise();

  msalInstance = instance;

  setAccessTokenProvider(async () => {
    const accounts = instance.getAllAccounts();
    if (accounts.length === 0) return null;

    try {
      const result = await instance.acquireTokenSilent({
        ...buildLoginRequest(),
        account: accounts[0],
        scopes: [getApiScope()],
      });
      return result.accessToken;
    } catch {
      // Silent failure: caller gets 401; user can Sign in again (redirect).
      return null;
    }
  });

  return instance;
}
