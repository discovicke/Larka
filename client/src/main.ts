import {createApp} from 'vue'
import App from './App.vue'
import {initializeTheme} from './composables/useTheme'
import {router} from './router'

import './assets/index.css'

initializeTheme()
createApp(App).use(router).mount('#app')
