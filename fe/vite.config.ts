import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, import.meta.dirname, '')
  const apiTarget = env.VITE_DEV_API_PROXY_TARGET || 'http://localhost:8080'
  const apiProxy = {
    '/api': { target: apiTarget, changeOrigin: true },
    '/hubs': { target: apiTarget, changeOrigin: true, ws: true },
  }

  return {
    cacheDir: 'node_modules/.vite-cache',
    plugins: [
      react(),
      tailwindcss(),
      VitePWA({
        registerType: 'autoUpdate',
        includeAssets: ['pwa-192.png', 'pwa-512.png'],
        manifest: {
          id: '/',
          name: 'AutoFleet Pro',
          short_name: 'AutoFleet',
          description: 'Vehicle rental operations and booking management.',
          theme_color: '#147e8c',
          background_color: '#f8fafc',
          display: 'standalone',
          start_url: '/',
          icons: [
            { src: '/pwa-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
            { src: '/pwa-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
            { src: '/pwa-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
          ],
        },
        workbox: {
          navigateFallbackDenylist: [/^\/api\//, /^\/hubs\//],
        },
      }),
    ],
    resolve: {
      alias: {
        '@': import.meta.dirname + '/src',
      },
    },
    server: {
      host: '0.0.0.0',
      port: 3000,
      strictPort: true,
      open: false,
      proxy: apiProxy,
    },
    preview: {
      host: '0.0.0.0',
      port: 3000,
      strictPort: true,
      proxy: apiProxy,
    },
  }
})
