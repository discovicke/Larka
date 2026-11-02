import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

function backend() {
  let name = 'Christian Gennari'
  mockApi((method, path, body) => {
    if (method === 'PUT' && path === '/api/me/profile') {
      name = (body as {name: string}).name
      return {status: 200, body: meAs('Student', [], name)}
    }
    if (path === '/api/me') {
      return {status: 200, body: meAs('Student', [], name)}
    }
    if (path === '/api/me/notification-settings') {
      return {status: 200, body: {muted: []}}
    }
    if (path === '/api/courses') {
      return {status: 200, body: []}
    }
    return {status: 404}
  })
}

describe('WelcomeView', () => {
  it('fyller i namnet från Microsoft och visar notiserna', async () => {
    backend()

    const {wrapper} = await renderApp('/valkommen')

    expect((wrapper.find('[data-test="welcome-name"]').element as HTMLInputElement).value).toBe('Christian Gennari')
    expect(wrapper.text()).toContain('Välkommen till Lärka')
    expect(wrapper.text()).toContain('Nyheter')
  })

  it('sparar ett ändrat namn och går vidare till sidan man ville till', async () => {
    backend()
    const {wrapper, router} = await renderApp('/valkommen?redirect=/inbox')

    await wrapper.find('[data-test="welcome-name"]').setValue('Chris Gennari')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('PUT', '/api/me/profile')).toEqual({name: 'Chris Gennari'})
    expect(router.currentRoute.value.fullPath).toBe('/inbox')
  })

  it('sparar inget när namnet stämmer', async () => {
    backend()
    const {wrapper, router} = await renderApp('/valkommen')

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('PUT', '/api/me/profile')).toBeUndefined()
    expect(router.currentRoute.value.name).toBe('today')
  })
})
