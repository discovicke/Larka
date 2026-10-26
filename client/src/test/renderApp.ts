import {enableAutoUnmount, flushPromises, mount} from '@vue/test-utils'
import {afterEach} from 'vitest'
import {createMemoryHistory, type Router} from 'vue-router'

import App from '@/App.vue'
import {createAppRouter} from '@/router'

enableAutoUnmount(afterEach)

/** Monterar hela appen på sökvägen, som i webbläsaren. */
export async function renderApp(path: string, router: Router = createAppRouter(createMemoryHistory())) {
  await router.push(path)
  // Fäst i dokumentet så att <PageActions> (Teleport) och dialoger hittar sina mål.
  const wrapper = mount(App, {global: {plugins: [router]}, attachTo: document.body})
  // Lazy-laddade sidor och API-anrop i flera led behöver några varv.
  for (let i = 0; i < 5; i++) {
    await flushPromises()
  }
  return {wrapper, router}
}
