import {flushPromises} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'

import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

describe('GitHubOnboardingBanner', () => {
  beforeEach(() => sessionStorage.clear())

  it('visar koppla GitHub för elev när skolan använder GitHub', async () => {
    mockApi((_method, path) =>
      path === '/api/me'
        ? {status: 200, body: meAs('Student', ['c1'], 'Bo Elev', null, true, true)}
        : {status: 404},
    )

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="github-onboarding"]').exists()).toBe(true)
    await wrapper.find('[data-test="github-onboarding-dismiss"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test="github-onboarding"]').exists()).toBe(false)
    expect(sessionStorage.getItem('github_onboarding_dismissed')).toBe('1')
  })

  it('visar inte bannern när skolan har GitHub men ingen av elevens kurser använder det', async () => {
    mockApi((_method, path) =>
      path === '/api/me'
        ? {status: 200, body: meAs('Student', ['c1'], 'Bo Elev', null, true, false)}
        : {status: 404},
    )

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="github-onboarding"]').exists()).toBe(false)
  })

  it('visar inte bannern när eleven redan kopplat GitHub', async () => {
    mockApi((_method, path) =>
      path === '/api/me'
        ? {status: 200, body: meAs('Student', ['c1'], 'Bo Elev', 'bo-elev', true, true)}
        : {status: 404},
    )

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-test="github-onboarding"]').exists()).toBe(false)
  })
})
