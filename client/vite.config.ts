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
          // Vites hjälpare för dynamiska importer måste ligga i vendor. Annars kan Rollup lägga den i msal-chunken,
          // och då laddas hela MSAL vid varje sidladdning.
          if (id.includes('vite/preload-helper')) {
            return 'vendor'
          }
          if (/node_modules\/(@tiptap|prosemirror|linkifyjs|markdown-it|marked)/.test(id)) {
            return 'editor'
          }
          // MSAL laddas bara när någon loggar in med Microsoft, så den får en egen chunk i stället för vendor.
          if (id.includes('node_modules/@azure/msal-')) {
            return 'msal'
          }
          // Ikonerna är den största delen av vendor och växer med varje ny ikon, så de får en egen chunk.
          if (id.includes('node_modules/@phosphor-icons/')) {
            return 'icons'
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
