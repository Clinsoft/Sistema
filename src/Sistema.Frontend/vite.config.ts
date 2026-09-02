import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [
    vue(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  build: {
    // Vuetify sozinho já é grande; com o split abaixo cada vendor vira um chunk
    // cacheável. 900 kB evita o alarme falso sem esconder um chunk realmente gordo.
    chunkSizeWarningLimit: 900,
    rollupOptions: {
      output: {
        // Separa os vendors pesados do código do app (carrega o essencial primeiro
        // e mantém as libs em cache entre deploys). As telas já são lazy por rota.
        // Só separa os vendors SEMPRE usados (Vuetify e core do Vue). Libs pesadas
        // e sob demanda (xlsx, zxing/QR) NÃO entram aqui — continuam lazy, no chunk
        // da tela que as importa, para não inflar o carregamento inicial.
        manualChunks(id: string) {
          if (!id.includes('node_modules')) return
          if (id.includes('vuetify')) return 'vuetify'   // antes de 'vue' (vuetify contém "vue")
          if (id.includes('/@mdi/')) return 'mdi'
          if (id.includes('/vue-router/') || id.includes('/pinia/')
              || id.includes('/@vue/') || id.includes('/vue/')) return 'vue-core'
          // resto: Vite decide (mantém dynamic imports em chunks próprios)
        }
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5131',
        changeOrigin: true,
        secure: false
      }
    }
  }
})
