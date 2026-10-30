import {fileURLToPath, URL} from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import {defineConfig} from 'vite'

// Backend-adressen från backend/Properties/launchSettings.json (profilen "http").
const backendUrl = process.env.BACKEND_URL ?? 'http://localhost:5257'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      output: {
        // Annars hamnar allt i en chunk på 1,4 MB. Biblioteken ändras sällan, så de cachas också längre så här.
        manualChunks(id) {
          if (/node_modules\/(@tiptap|prosemirror|linkifyjs|markdown-it|marked)/.test(id)) {
            return 'editor'
          }
          if (id.includes('node_modules')) {
            return 'vendor'
          }
        },
      },
    },
  },
  server: {
    // Vite skickar API-anropen vidare till backend, så webbläsaren pratar bara med en origin (ingen CORS).
    proxy: {
      '/api': backendUrl,
      '/health': backendUrl,
      '/version': backendUrl,
    },
  },
})
