import {flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import HomeView from './HomeView.vue'

function stubFetch(response: Response | Error) {
  vi.stubGlobal(
    'fetch',
    vi.fn(() => (response instanceof Error
      ? Promise.reject(response)
      : Promise.resolve(response))),
  )
}

describe('HomeView', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renderar rubriken', () => {
    stubFetch(new Response('{}', {headers: {'Content-Type': 'application/json'}}))
    const wrapper = mount(HomeView)

    expect(wrapper.find('h1').text()).toBe('Welcome to your new project')
  })

  it('visar version från API:et', async () => {
    stubFetch(
      new Response(JSON.stringify({version: '1.2.3', commit: 'abc123'}), {
        headers: {'Content-Type': 'application/json'},
      }),
    )
    const wrapper = mount(HomeView)
    await flushPromises()

    expect(wrapper.find('[data-test="api-status"]').text()).toBe('API: OK (1.2.3, abc123)')
  })

  it('visar fel när backend inte svarar', async () => {
    stubFetch(new TypeError('Failed to fetch'))
    const wrapper = mount(HomeView)
    await flushPromises()

    expect(wrapper.find('[data-test="api-status"]').text()).toBe('API: could not reach the backend')
  })
})
