import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach} from 'vitest'
import {createMemoryHistory, type Router} from 'vue-router'

import App from '@/App.vue'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useNotifications} from '@/composables/useNotifications'
import {createAppRouter} from '@/router'
import {signInForTests} from '@/test/auth'

enableAutoUnmount(afterEach)

/** Monterar hela appen på sökvägen, som i webbläsaren. */
export async function renderApp(path: string, router: Router = createAppRouter(createMemoryHistory())) {
  // Användaren ligger i en delad modul; varje test ska hämta den från sin egen mock.
  useCurrentUser().resetCurrentUser()
  useNotifications().resetNotifications()
  // Appen går bara att öppna när man är inloggad, så testen loggar in en låtsasanvändare först.
  signInForTests()
  await router.push(path)
  // Fäst i dokumentet så att <PageActions> (Teleport) och dialoger hittar sina mål.
  const wrapper = mount(App, {global: {plugins: [router]}, attachTo: document.body})
  // Lazy-laddade sidor och API-anrop i flera led behöver några varv.
  for (let i = 0; i < 5; i++) {
    await flushPromises()
  }
  return {wrapper, router}
}
