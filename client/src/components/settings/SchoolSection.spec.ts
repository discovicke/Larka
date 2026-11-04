import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'

import {useCurrentUser} from '@/composables/useCurrentUser'
import {mockApi, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import SchoolSection from './SchoolSection.vue'

// #521: admin byter skolans namn under Inställningar.
function backend(status = 200) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Admin')}
    }
    if (method === 'PUT' && path === '/api/schools/s1') {
      return status === 200
        ? {status: 200, body: {id: 's1', ...(body as object)}}
        : {status, body: 'name must be 1 to 200 characters'}
    }
    return {status: 404}
  })
}

async function mountSection() {
  await useCurrentUser().loadMe()
  const wrapper = mount(SchoolSection)
  await flushPromises()
  return wrapper
}

async function rename(wrapper: Awaited<ReturnType<typeof mountSection>>, value: string) {
  await wrapper.find('[data-test="school-name-input"]').setValue(value)
  await wrapper.find('[data-test="school-name-form"]').trigger('submit')
  await flushPromises()
}

describe('SchoolSection', () => {
  beforeEach(() => {
    useCurrentUser().resetCurrentUser()
  })

  it('visar skolans namn och sparar ett nytt', async () => {
    backend()
    const wrapper = await mountSection()

    expect((wrapper.find('[data-test="school-name-input"]').element as HTMLInputElement).value).toBe('Edugrade')
    await rename(wrapper, '  Lärkskolan ')

    expect(sent('PUT', '/api/schools/s1')).toEqual({name: 'Lärkskolan'})
    expect(useCurrentUser().me.value?.school.name).toBe('Lärkskolan')
  })

  it('visar serverns fel på svenska', async () => {
    backend(400)
    const wrapper = await mountSection()

    await rename(wrapper, 'Ny skola')

    expect(wrapper.find('[data-test="school-name-error"]').text()).toContain('Namnet måste vara mellan 1 och 200 tecken.')
  })

  it('säger till när namnet är tomt', async () => {
    backend()
    const wrapper = await mountSection()

    await rename(wrapper, '  ')

    expect(wrapper.find('[data-test="school-name-error"]').text()).toContain('Skriv skolans namn.')
    expect(sent('PUT', '/api/schools/s1')).toBeUndefined()
  })
})
