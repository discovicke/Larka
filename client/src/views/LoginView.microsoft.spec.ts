import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it, vi} from 'vitest'
import {createMemoryHistory, createRouter} from 'vue-router'

import {AuthError} from '@/lib/auth'
import {signOutForTests} from '@/test/auth'

import LoginView from './LoginView.vue'

const microsoft = vi.hoisted(() => ({
  fetchMicrosoftSettings: vi.fn(),
  startMicrosoftSignIn: vi.fn(),
  finishMicrosoftSignIn: vi.fn(),
}))

vi.mock('@/lib/auth/microsoft', () => microsoft)

const settings = {clientId: 'client-1', tenantId: 'tenant-1'}
const page = {template: '<div/>'}

async function render(path = '/login') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {path: '/login', name: 'login', component: LoginView},
      {path: '/', name: 'today', component: page},
      {path: '/inbox', name: 'inbox', component: page},
      {path: '/valkommen', name: 'welcome', component: page},
    ],
  })
  await router.push(path)
  const wrapper = mount(LoginView, {global: {plugins: [router]}})
  await flushPromises()
  return {wrapper, router}
}

beforeEach(() => {
  signOutForTests()
  microsoft.fetchMicrosoftSettings.mockReset().mockResolvedValue(settings)
  microsoft.startMicrosoftSignIn.mockReset().mockResolvedValue(undefined)
  microsoft.finishMicrosoftSignIn.mockReset().mockResolvedValue(null)
  // Som när Microsoft skickar tillbaka till /login. Testerna utan svar tar bort det.
  window.location.hash = '#code=abc'
})

describe('LoginView med Microsoft', () => {
  it('visar Logga in med Microsoft först och lösenordet under', async () => {
    const {wrapper} = await render()

    expect(wrapper.find('[data-test="microsoft-sign-in"]').text()).toContain('Logga in med Microsoft')
    expect(wrapper.text()).toContain('eller med lösenord')
    expect(wrapper.find('#login-password').exists()).toBe(true)
  })

  it('visar bara lösenordet när skolan inte har Microsoft', async () => {
    microsoft.fetchMicrosoftSettings.mockResolvedValue(null)

    const {wrapper} = await render()

    expect(wrapper.find('[data-test="microsoft-sign-in"]').exists()).toBe(false)
    expect(microsoft.finishMicrosoftSignIn).not.toHaveBeenCalled()
  })

  it('tar inte emot något när sidan inte öppnades av Microsoft', async () => {
    window.location.hash = ''

    const {wrapper} = await render()

    expect(microsoft.finishMicrosoftSignIn).not.toHaveBeenCalled()
    expect(wrapper.find('[data-test="microsoft-sign-in"]').attributes('disabled')).toBeUndefined()
  })

  it('knappen skickar till Microsoft med sidan man ville till', async () => {
    const {wrapper} = await render('/login?redirect=/inbox')

    await wrapper.find('[data-test="microsoft-sign-in"]').trigger('click')
    await flushPromises()

    expect(microsoft.startMicrosoftSignIn).toHaveBeenCalledWith(settings, '/inbox')
  })

  it('tillbaka från Microsoft öppnas sidan man ville till', async () => {
    microsoft.finishMicrosoftSignIn.mockResolvedValue({firstSignIn: false, returnTo: '/inbox'})

    const {router} = await render()

    expect(router.currentRoute.value.fullPath).toBe('/inbox')
  })

  it('första gången går det via välkomstsidan', async () => {
    microsoft.finishMicrosoftSignIn.mockResolvedValue({firstSignIn: true, returnTo: '/inbox'})

    const {router} = await render()

    expect(router.currentRoute.value.name).toBe('welcome')
    expect(router.currentRoute.value.query.redirect).toBe('/inbox')
  })

  it('en sida utanför appen i state ger startsidan', async () => {
    microsoft.finishMicrosoftSignIn.mockResolvedValue({firstSignIn: false, returnTo: '//evil.example'})

    const {router} = await render()

    expect(router.currentRoute.value.fullPath).toBe('/')
  })

  it('visar API:ets text när Lärka säger nej', async () => {
    microsoft.finishMicrosoftSignIn.mockRejectedValue(new AuthError(403, 'Ditt konto är spärrat.'))

    const {wrapper, router} = await render()

    expect(wrapper.find('[role="alert"]').text()).toBe('Ditt konto är spärrat.')
    expect(router.currentRoute.value.name).toBe('login')
  })
})
