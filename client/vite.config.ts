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
    // Editorn (Tiptap) är en egen chunk på drygt 500 kB som bara laddas på sidan för att skapa innehåll.
    chunkSizeWarningLimit: 600,
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
