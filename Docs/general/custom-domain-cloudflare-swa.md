# Custom domain — Cloudflare DNS + Azure SWA (`code-smith.cc`)

**Date:** 2026-08-12  
**Audience:** You (Cloudflare + Azure + Entra + Stripe portal/env steps)  
**Scope:** SPA custom domain only. API stays on the Container App default hostname.  
**Prerequisite:** Domain `code-smith.cc` purchased; Cloudflare is authoritative DNS (CF nameservers active).

---

## Locked decisions

| Decision | Choice |
|----------|--------|
| Cloudflare role | DNS (+ registrar). **No** orange-cloud in front of the SPA or API |
| Public hosts | Canonical SPA: `https://www.code-smith.cc` |
| Apex | `https://code-smith.cc` → **301 redirect** to `https://www.code-smith.cc` (CF handles redirect only) |
| API hostname | Unchanged (`*.azurecontainerapps.io`). No ACA custom domain, no `VITE_API_BASE_URL` change, no Stripe webhook URL change |
| Legacy SWA host | Keep `https://yellow-sand-03abd5710.7.azurestaticapps.net` working (Entra + CORS) for rollback |
| Certs | SWA **free managed certificate** on `www` |
| Repo code | **None** — ops/config only |

### Fan-out checklist (this cut)

| # | Where | What |
|---|--------|------|
| 1 | Cloudflare DNS | `www` → SWA (**DNS only** / grey cloud) |
| 2 | Cloudflare | Apex record + **Redirect Rule** apex → `www` |
| 3 | Azure SWA | Custom domain `www.code-smith.cc` + managed cert |
| 4 | API Container App env | `AllowedCorsOrigins__N` += `https://www.code-smith.cc` |
| 5 | Entra External ID | SPA app **CodeSmith.Web** redirect URI += `https://www.code-smith.cc` |
| 6 | API Container App env | `Stripe__SuccessUrl` / `Stripe__CancelUrl` → www account checkout query URLs |

**Explicitly out of scope:** ACA custom domain, `deploy-swa.yml` / `VITE_*` API base changes, Stripe webhook endpoint URL, Google Cloud OAuth client origins (federation callback remains `*.ciamlogin.com`).

---

## Current anchors (prod)

Confirm in portal if anything drifted:

| Surface | Value (as of prior ops docs) |
|---------|------------------------------|
| SWA | `swa-codesmith-prod-centralus-001` |
| Default SWA hostname | `https://yellow-sand-03abd5710.7.azurestaticapps.net` |
| API | Container App `ca-codesmith-api-001` (resource group `rg-codesmith-prod-centralus-001`) |
| Stripe return paths | `/account?checkout=success` and `/account?checkout=cancel` |
| Entra SPA app | `CodeSmith.Web` (SPA platform redirects) |
| CIAM host | `https://codesmithapp.ciamlogin.com/` |

---

## Order of operations

Do **not** rearrange casually. SWA domain validation needs the DNS record visible; auth/billing break if CORS or Entra lag behind the first real browser hit on www.

1. Cloudflare: create **`www` CNAME** (grey-cloud) → default SWA hostname  
2. Azure SWA: **Add custom domain** `www.code-smith.cc` (complete TXT/CNAME validation if prompted)  
3. Wait until SWA shows domain **Ready** + managed cert **Issued**  
4. Cloudflare: apex → www **redirect** (see below)  
5. Container App: add CORS origin for `https://www.code-smith.cc`  
6. Entra: add SPA redirect URI `https://www.code-smith.cc` (and `http://localhost:5173` / SWA default remain)  
7. Container App: update Stripe success/cancel URLs to www  
8. Smoke test (section below)  
9. Optional later: soft-retire default SWA host from Entra + CORS (not required now)

---

## Phase 1 — Cloudflare DNS for `www`

1. Open [Cloudflare dashboard](https://dash.cloudflare.com) → zone **`code-smith.cc`**.
2. **DNS** → **Records** → **Add record**:
   - **Type:** `CNAME`
   - **Name:** `www`
   - **Target:** `yellow-sand-03abd5710.7.azurestaticapps.net`  
     (no `https://`; use the current default SWA hostname if it ever changes)
   - **Proxy status:** **DNS only** (grey cloud) — **required** so SWA managed cert + hostname validation work
   - **TTL:** Auto
3. Save.

Do **not** orange-cloud `www`. Proxied `www` fights Azure’s managed certificate and custom-domain validation.

### Validation TXT (only if SWA asks)

When you add the domain in SWA, Azure may show a **TXT** host/value (often `asuid.www` or similar):

1. Cloudflare → **DNS** → **Add record** → **TXT** with the **exact** host and value SWA displays.
2. Wait for DNS (usually minutes; can be longer).
3. Click validate / refresh in the SWA custom-domain blade.

---

## Phase 2 — SWA custom domain + managed cert

1. Azure Portal → Static Web App **`swa-codesmith-prod-centralus-001`**.
2. **Custom domains** → **Add** → **Custom domain on other DNS**.
3. Enter **`www.code-smith.cc`**.
4. Complete the CNAME/TXT steps Azure shows (Phase 1).
5. Choose / confirm **managed certificate** (free).
6. Wait until status is healthy (**Ready** / cert **Issued**).  
   First issue can take a while after DNS is correct; do not proceed to auth smoke on www until HTTPS loads without cert warnings.

**Do not** bind the bare apex `code-smith.cc` as a second live SWA host. Apex is redirect-only (Phase 3).

---

## Phase 3 — Apex redirect (`code-smith.cc` → `www`)

Goal: anyone who types the bare domain lands on the canonical origin. CF may **proxy the apex only** so it can answer HTTP(S) and emit the redirect. That is **not** putting CF in front of the SPA (`www` stays grey-cloud → SWA).

### 3a. Apex DNS record (so CF can answer `@`)

Cloudflare must have a proxied apex record for redirect rules to run on HTTPS. Common pattern:

1. **DNS** → **Add record**:
   - **Type:** `AAAA` (or use CF’s current recommended “redirect apex” placeholder)
   - **Name:** `@` (apex)
   - **IPv6:** `100::` (CF placeholder used with redirect rules; traffic is not sent to a real origin)
   - **Proxy status:** **Proxied** (orange cloud) — apex **only**
2. Save.

If Cloudflare UI offers a bulk-redirect / “redirect bare domain to www” wizard, that is equivalent; keep **`www` DNS-only**.

### 3b. Single Redirect Rule

1. Cloudflare → **Rules** → **Redirect Rules** (or **Rules** → **Overview** → Create redirect).
2. Create rule, e.g. name `apex-to-www`.
3. **If** (custom filter expression), roughly:

   ```text
   http.host eq "code-smith.cc"
   ```

4. **Then** URL redirect:
   - **Type:** Dynamic (or Static if you only care about `/`)
   - **Expression / target:** `concat("https://www.code-smith.cc", http.request.uri)`  
     (Static alternative: `https://www.code-smith.cc` only — loses path; prefer keeping path)
   - **Status:** `301` (permanent)
5. Deploy / save.

### 3c. Quick DNS check

```bash
# www must be CNAME (or resolve) to SWA; should NOT be CF anycast if grey-cloud
nslookup www.code-smith.cc

# apex should hit Cloudflare (proxied) when orange-cloud
nslookup code-smith.cc
```

Browser: `https://code-smith.cc` → address bar becomes `https://www.code-smith.cc/...`.

---

## Phase 4 — API CORS

Browser calls from the new origin need an exact allow-list match (no trailing slash).

1. Azure Portal → Container App **`ca-codesmith-api-001`** → **Containers** / **Environment variables** (active revision).
2. List existing `AllowedCorsOrigins__0`, `__1`, … (localhost and default SWA origin should already exist).
3. Add the next index, e.g. if `0` and `1` are taken:

   | Name | Value |
   |------|--------|
   | `AllowedCorsOrigins__2` | `https://www.code-smith.cc` |

4. Create a **new revision** / restart so the env is picked up.

**Do not** add a trailing slash.  
**Do not** remove the default SWA origin yet (rollback).  
**Do not** add `https://code-smith.cc` unless you re-open dual-host (we redirect apex away).

ASP.NET binds arrays via `AllowedCorsOrigins__N` only — a single comma-separated string fails silently.

---

## Phase 5 — Entra External ID (SPA redirects)

1. [Entra admin center](https://entra.microsoft.com) → correct **External ID / CIAM** tenant.
2. **App registrations** → **CodeSmith.Web** (SPA).
3. **Authentication** → **Single-page application** redirect URIs → **Add**:

   - `https://www.code-smith.cc`

4. Keep existing:

   - `http://localhost:5173` (or `https://localhost:5173` if that is what you registered)
   - `https://yellow-sand-03abd5710.7.azurestaticapps.net`

5. Save.

No change to API app registration audience/scopes for this cut.  
Google IdP: **no** Google Cloud “Authorized JavaScript origins” change required — browser OAuth for Google is via CIAM (`ciamlogin.com`), not the SPA origin.

If you configured **Front-channel logout** or explicit **post-logout** URIs on the SPA app, add the www origin there too (mirror whatever you already have for the default SWA host).

---

## Phase 6 — Stripe success / cancel URLs

Checkout return URLs are Container App env (not SPA build vars).

1. Container App **`ca-codesmith-api-001`** env:

   | Name | Value |
   |------|--------|
   | `Stripe__SuccessUrl` | `https://www.code-smith.cc/account?checkout=success` |
   | `Stripe__CancelUrl` | `https://www.code-smith.cc/account?checkout=cancel` |

2. New revision / confirm active revision shows both values.

**Webhook:** leave endpoint on the **existing API** public URL (`https://<aca-host>/api/billing/webhook`). Do not point Stripe at `www` (SWA has no API). No webhook secret rotation required for this cut.

---

## Phase 7 — Smoke checklist

Run in order on a clean browser profile or private window.

| # | Check | Pass criteria |
|---|--------|----------------|
| 1 | `https://www.code-smith.cc` | SPA loads; padlock valid (SWA managed cert) |
| 2 | `https://code-smith.cc` | 301/redirect to `https://www.code-smith.cc` |
| 3 | `https://yellow-sand-03abd5710.7.azurestaticapps.net` | Still loads (rollback host) |
| 4 | Sign in (email) on **www** | CIAM round-trip returns to www; no redirect URI mismatch error |
| 5 | Sign in (Google) on **www** | Same; reaches Google via CIAM |
| 6 | Authenticated API call (e.g. quota / start session) | No CORS `NetworkError`; no browser missing `Access-Control-Allow-Origin` |
| 7 | Account → credit packs | Catalog loads (Stripe secret still OK) |
| 8 | Sandbox checkout (optional) | Stripe returns to www `/account?checkout=success` or cancel; webhook still 200 on ACA URL |

### Failure cheatsheet

| Symptom | Likely cause |
|---------|----------------|
| SWA domain stuck validating | `www` orange-cloud, wrong CNAME target, or missing `asuid` TXT |
| Cert error on www | Managed cert not issued yet; or www proxied through CF |
| `AADSTS50011` / redirect URI mismatch | Entra SPA platform missing exact `https://www.code-smith.cc` |
| `NetworkError` / CORS on API | Missing `AllowedCorsOrigins__N` = `https://www.code-smith.cc` (exact, no slash); old revision |
| Checkout returns to old host | `Stripe__SuccessUrl` / `CancelUrl` not updated on active revision |
| Webhook failures after unrelated changes | Webhook URL should still be ACA — do not “fix” it to www |

---

## Rollback

1. Share / bookmark the default SWA URL again.  
2. Stripe success/cancel → point back at default SWA `/account?checkout=…` if needed.  
3. Leave Entra + CORS entries for www in place (harmless) or remove later.  
4. Cloudflare: disable apex redirect rule; delete or grey-cloud records as desired.  
5. SWA: remove custom domain binding when DNS no longer points at it (optional).

---

## Out of scope / later

- Custom domain on **API** Container App (`api.code-smith.cc`) — only if you want branded API/webhook URLs; then update `VITE_API_BASE_URL`, redeploy SWA, CORS (unchanged origins), Stripe **webhook** endpoint, ACA cert validation.
- Soft-retire default SWA hostname (remove from Entra + CORS) after www is stable.
- Orange-cloud CDN/WAF in front of SWA (rejected for this project cut).
- Entra **custom authentication domain** / Google “continue to …” branding (still `ciamlogin.com` unless you buy that separate project).

---

## Related docs

- `Docs/general/entra-external-id-azure-setup.md` — CIAM + SPA app registration  
- `Docs/Recaps/2026-07-13-increment-1-frontend-hosting-auth.md` — SWA + CORS lessons  
- `Docs/Recaps/2026-08-10-stripe-prod-sandbox-purchase-setup.md` — Stripe env binding names  
- `context.md` — CORS, billing, deploy topology  
