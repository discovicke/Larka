import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests} from '@/test/mockApi'
import StudentHelpButton from './StudentHelpButton.vue'

describe('StudentHelpButton', () => {
  it('skickar och tar tillbaka en hjälpfråga', async () => {
    let requested = false
    mockApi((method, path) => {
      if (path === '/api/links/l1/help' && method === 'GET') {
        return {
          status: 200,
          body: {requested, message: null, createdAt: null, resolved: false},
        }
      }
      if (path === '/api/links/l1/help' && method === 'POST') {
        requested = true
        return {
          status: 200,
          body: {
            requested: true,
            message: null,
            createdAt: '2026-10-30T10:00:00Z',
            resolved: false,
          },
        }
      }
      if (path === '/api/links/l1/help' && method === 'DELETE') {
        requested = false
        return {status: 204}
      }
      return {status: 404}
    })

    const wrapper = mount(StudentHelpButton, {props: {linkId: 'l1'}})
    await flushPromises()

    await wrapper.find('[data-test="student-help-request"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test="student-help-withdraw"]').exists()).toBe(true)
    expect(requests.some((request) => request.method === 'POST')).toBe(true)

    await wrapper.find('[data-test="student-help-withdraw"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test="student-help-request"]').exists()).toBe(true)
    expect(requests.some((request) => request.method === 'DELETE')).toBe(true)
  })
})
