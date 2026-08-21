import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      // Forwards /api and /health to the WarehouseApi backend so the browser sees
      // everything as same-origin (http://localhost:5173) in development. That means:
      //  - no CORS configuration needed for local dev
      //  - the refresh-token cookie is a plain same-site cookie, not a cross-site one,
      //    so it works over plain HTTP without fighting browser third-party-cookie policy
      // `secure: false` trusts the API's self-signed local HTTPS dev certificate.
      '/api': {
        target: 'https://localhost:5443',
        changeOrigin: true,
        secure: false,
      },
      '/health': {
        target: 'https://localhost:5443',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
