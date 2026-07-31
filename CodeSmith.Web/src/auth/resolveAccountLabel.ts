// == Account label resolution == //
import type { AccountInfo } from "@azure/msal-browser";

const FALLBACK_LABEL = "Signed in";
const PLACEHOLDER_NAME = "unknown";                    // Entra's display name for users whose flow collects none

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// Federated (social) sign-ins get a synthesized {objectId}@{tenant}.onmicrosoft.com UPN, which MSAL
// surfaces as account.username via the preferred_username claim. It is email-shaped but is not an
// email — rendering it shows the user a raw GUID.
const SYNTHESIZED_UPN_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}@/i;

/**
 * Resolves the human-readable label for a signed-in account, preferring the verified email claim and
 * refusing the two values Entra substitutes when it has nothing better: the synthesized GUID UPN and
 * the literal "unknown" display name.
 */
export function resolveAccountLabel(account: AccountInfo | undefined): string {
  const email = usable(readEmailClaim(account));
  if (email) return email;

  const username = usable(account?.username);
  if (username && EMAIL_PATTERN.test(username) && !SYNTHESIZED_UPN_PATTERN.test(username)) {
    return username;
  }

  const name = usable(account?.name);
  if (name && name.toLowerCase() !== PLACEHOLDER_NAME) return name;

  return FALLBACK_LABEL;
}

// The email claim is not part of MSAL's declared TokenClaims, so it arrives through the index
// signature as unknown and has to be narrowed before use.
function readEmailClaim(account: AccountInfo | undefined): string | undefined {
  const claim = account?.idTokenClaims?.["email"];
  return typeof claim === "string" ? claim : undefined;
}

// MSAL yields "" rather than undefined when no claim matches, so blank-but-present must be treated
// as absent for the fallback chain to advance.
function usable(value: string | undefined): string | undefined {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}
