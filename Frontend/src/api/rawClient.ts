import axios from 'axios'

/**
 * A plain axios instance with no interceptors — used only for the auth endpoints
 * (login, register, refresh, revoke). Kept separate from the authenticated `client`
 * in client.ts so the 401-retry interceptor never accidentally recurses into itself
 * while trying to refresh.
 *
 * `withCredentials: true` is what makes the browser send/receive the httpOnly refresh
 * cookie. The custom header is a lightweight CSRF guard the API's /refresh endpoint
 * requires — see WarehouseApi/Endpoints/AuthEndpoints.cs for why.
 */
export const rawClient = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '',
  withCredentials: true,
  headers: {
    'X-Requested-With': 'warehouse-web',
  },
})
