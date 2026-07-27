// == MSAL / Entra External ID configuration == //
import type { Configuration } from "@azure/msal-browser";
import { LogLevel } from "@azure/msal-browser";

export function isMsalConfigured(): boolean {
  return Boolean(
    import.meta.env.VITE_AAD_CLIENT_ID &&
      import.meta.env.VITE_AAD_TENANT_ID &&
      import.meta.env.VITE_AAD_INSTANCE &&
      import.meta.env.VITE_AAD_API_SCOPE
  );
}

export function getApiScope(): string {
  return (import.meta.env.VITE_AAD_API_SCOPE as string).trim();
}

export function buildMsalConfig(): Configuration {
  const clientId = (import.meta.env.VITE_AAD_CLIENT_ID as string).trim();
  const tenantId = (import.meta.env.VITE_AAD_TENANT_ID as string).trim();
  const instance = (import.meta.env.VITE_AAD_INSTANCE as string).trim().replace(/\/$/, "");

  // CIAM authority: https://{tenant}.ciamlogin.com/{tenantId}
  const authority = `${instance}/${tenantId}`;

  return {
    auth: {
      clientId,
      authority,
      knownAuthorities: [new URL(instance).hostname],
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin,
    },
    cache: {
      cacheLocation: "sessionStorage",
    },
    system: {
      loggerOptions: {
        logLevel: LogLevel.Warning,
        piiLoggingEnabled: false,
      },
    },
  };
}

export function buildLoginRequest() {
  return {
    scopes: [getApiScope()],
  };
}

// == Google federated IdP (CIAM domain_hint) == //
// Capital "Google": lowercase "google" fails on desktop browser CIAM authorize
// (AADSTS500208 / AADSTS90023) while hosted "Sign in with Google" still works.
export function buildGoogleLoginRequest() {
  return {
    ...buildLoginRequest(),
    extraQueryParameters: { domain_hint: "Google" },
  };
}
