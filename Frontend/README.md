# Warehouse Web

A React + Vite frontend for the [WarehouseApi](../WarehouseApi) backend: login/register,
role-gated inventory management (Viewer/Manager/Admin), and an auth flow built around
the API's httpOnly-cookie refresh tokens.

## Stack

- **Vite 8 + React 19 + TypeScript** (strict mode, `verbatimModuleSyntax`)
- **React Router 7** for routing and route guards
- **Tailwind CSS v4** for styling — a small custom design-token set in `src/index.css`
  (warehouse-signage amber accent, dark slate sidebar, a recurring "shelf tag" motif
  used for role badges) rather than default Tailwind blue-gray
- **Axios** for HTTP, with a hand-rolled 401 → refresh → retry interceptor
- No state management library — React Context + hooks is enough at this size

## Running it

You need the WarehouseApi backend running first (see its own README). Then:

```bash
npm install
npm run dev
```

Open `http://localhost:5173`. In development, Vite's dev server proxies `/api/*` and
`/health` straight to the backend (`https://localhost:5443` by default — see
`vite.config.ts`), so the browser sees everything as same-origin. That matters for the
auth flow: it means the refresh-token cookie behaves as an ordinary same-site cookie in
dev, with no CORS setup and no browser third-party-cookie policy to fight.

If you deploy the frontend and API on genuinely separate domains, set `VITE_API_URL`
(see `.env.example`) and make sure the backend's `Cors:AllowedOrigins` and cookie
`SameSite`/`Secure` settings agree with that split — see the comments in
`WarehouseApi/Endpoints/AuthEndpoints.cs`.

## How auth works here

This follows the pattern described alongside the backend template:

1. **Access token** — held in memory only (`src/auth/tokenStore.ts`, a plain module
   variable, not React state and not `localStorage`). It disappears on a full page
   reload, on purpose.
2. **Refresh token** — never touched by JavaScript. It's an httpOnly cookie the browser
   manages automatically; the frontend just calls `POST /api/auth/refresh` with
   `withCredentials: true` and gets a new access token back.
3. **On app load** (`AuthProvider` in `src/auth/AuthContext.tsx`), the app silently
   calls `/api/auth/refresh` before rendering anything auth-dependent, to restore the
   session from the cookie without asking the user to log in again.
4. **Proactive refresh** — the access token's `expiresAtUtc` (returned by the API) is
   used to schedule a refresh about 60 seconds before it actually expires, so routine
   use rarely hits an expired token at all.
5. **Reactive refresh** — an axios response interceptor (`src/api/client.ts`) catches
   any 401, refreshes once, and retries the original request. Concurrent 401s share a
   single in-flight refresh call instead of racing each other against the rotating
   refresh token. If the refresh itself fails, the user is signed out.
6. **The JWT is decoded client-side** (`src/lib/jwt.ts`) only to read `email`/`role`
   claims for UI purposes (nav visibility, the role badge). This is explicitly *not*
   a signature check — the browser has no way to verify a JWT signature, and isn't
   meant to. Every request is still independently authorized by the API.

## Role-based UI

`useAuth().hasRole(...)` gates navigation, the "Add item" button, and the Edit/Delete
row actions. Routes are wrapped in `<RequireAuth roles={[...]}>` so navigating directly
to `/products/new` as a Viewer redirects to a "forbidden" page instead of rendering a
broken form.

**This is UX, not security** — every comment near this logic says so on purpose. If
someone bypasses the UI and calls `POST /api/products` directly with a Viewer's token,
the API's `[ManagerOrAdmin]` policy rejects it regardless of what the frontend would
have shown. Never add a feature here that assumes the client-side role check is the
only thing standing between a user and an action.

## Project layout

```
src/
  api/           axios clients + typed calls (auth.ts, products.ts, types.ts)
  auth/          AuthContext, RequireAuth route guard, in-memory token store
  components/    Layout, RoleBadge, Spinner, ConfirmDialog, Toast
  pages/         Login, Register, Products (list), ProductForm (create/edit), errors
  lib/           jwt.ts (decode-only), apiError.ts (ProblemDetails → readable message)
```

## Security notes / honest limitations

- **XSS is still the thing to guard against most.** With the token in memory and the
  refresh token in an httpOnly cookie, a successful XSS can't steal long-lived
  credentials — but it *can* still ride along on authenticated requests while the page
  is open. Never use `dangerouslySetInnerHTML` on anything from the API without
  sanitizing it first; there's none in this template, but it's the first thing to
  audit if you add rich-text rendering later.
- **CSRF guard is minimal by design.** `/api/auth/refresh` requires a custom
  `X-Requested-With` header (see `rawClient.ts` / the backend's `AuthEndpoints.cs`) as
  a lightweight mitigation. It's proportionate for this template but not a substitute
  for a proper CSRF token if you add other cookie-authenticated, state-changing
  endpoints later.
- **No CSP is set by the frontend itself** — Vite's dev server doesn't, and a static
  production build's headers depend on whatever serves it (nginx, a CDN, etc.). Set a
  `Content-Security-Policy` at that layer; don't assume the API's CSP (which only
  covers its own JSON responses) protects the frontend's HTML.
- **`npm audit`** — run it periodically, same advice as the backend's `dotnet list
  package --vulnerable`. Frontend dependency compromises are a real, increasingly
  common attack vector because the compromised code runs directly in your users'
  browsers with full DOM/token access.
