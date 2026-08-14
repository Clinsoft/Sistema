import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createVuetify } from 'vuetify'
import { aliases, mdi } from 'vuetify/iconsets/mdi'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import { pt } from 'vuetify/locale'
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import './styles/responsive.css'

import App from './App.vue'
import router from './router'

const vuetify = createVuetify({
  components,
  directives,
  locale: { locale: 'pt', fallback: 'en', messages: { pt } },  // rótulos dos componentes em pt-BR
  icons: { defaultSet: 'mdi', aliases, sets: { mdi } },
  theme: {
    defaultTheme: 'ecoGranelLight',
    themes: {
      ecoGranelLight: {
        dark: false,
        colors: {
          primary:    '#5C2D0C',   // marrom-escuro EcoGranel
          secondary:  '#8B4513',   // marrom-médio
          accent:     '#6AAF2E',   // verde-folha
          error:      '#C62828',
          warning:    '#E65100',
          info:       '#0277BD',
          success:    '#3D7A1E',   // verde-escuro (broto)
          background: '#FAF7F4',   // off-white quente
          surface:    '#FFFFFF',
        }
      },
      ecoGranelDark: {
        dark: true,
        colors: {
          primary:    '#C8845A',
          secondary:  '#A0522D',
          accent:     '#8BC34A',
          error:      '#EF5350',
          warning:    '#FF7043',
          info:       '#29B6F6',
          success:    '#66BB6A',
          background: '#1A1008',
          surface:    '#2C1A0E',
        }
      }
    }
  }
})

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.use(vuetify)
app.mount('#app')
