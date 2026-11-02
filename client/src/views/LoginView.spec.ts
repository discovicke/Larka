import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'
import {createMemoryHistory, createRouter} from 'vue-router'

import {useCurrentUser} from '@/composables/useCurrentUser'
import {signOutForTests} from '@/test/auth'
import {meAs} from '@/test/me'
import {mockApi, sent} from '@/test/mockApi'

import LoginView from './LoginView.vue'

const redirectTarget = {template: '<div/>'}

function testRouter() {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      {path: '/login', name: 'login', component: LoginView},
      {path: '/', name: 'today', component: redirectTarget},
      {path: '/groups', name: 'groups', component: redirectTarget},
    ],
  })
}

async function render(path = '/login') {
  const router = testRouter()
  await router.push(path)
  const wrapper = mount(LoginView, {global: {plugins: [router]}})
  return {wrapper, router}
}

function tokenResponse() {
  return {
    status: 200,
    body: {accessToken: 'abc', expiresAt: new Date(Date.now() + 60_000).toISOString()},
  }
}

async function submitLogin(wrapper: ReturnType<typeof mount>, username: string, password: string) {
  await wrapper.find('#login-username').setValue(username)
  await wrapper.find('#login-password').setValue(password)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

beforeEach(() => {
  signOutForTests()
})

describe('LoginView', () => {
  it('visar formuläret utan Microsoft-knapp', async () => {
    mockApi(() => ({status: 404}))

    const {wrapper} = await render()

    expect(wrapper.find('h2').text()).toBe('Logga in')
    expect(wrapper.find('#login-username').attributes('type')).toBe('text')
    expect(wrapper.find('#login-username').attributes('autocomplete')).toBe('username')
    expect(wrapper.find('#login-password').attributes('type')).toBe('password')
    expect(wrapper.find('#login-password').attributes('autocomplete')).toBe('current-password')
    expect(wrapper.find('label[for="login-username"]').text()).toBe('Användarnamn eller e-post')
    expect(wrapper.find('button[type="submit"]').text()).toBe('Logga in')
    expect(wrapper.text()).not.toContain('Microsoft')
  })

  it('skickar användarnamn och lösenord', async () => {
    mockApi((_method, path) => path === '/api/auth/login'
      ? tokenResponse()
      : {status: 404})
    const {wrapper} = await render()

    await submitLogin(wrapper, 'larare', 'larare')

    expect(sent('POST', '/api/auth/login')).toEqual({username: 'larare', password: 'larare'})
  })

  it('visar fel e-post eller lösenord vid 401', async () => {
    mockApi(() => ({status: 401, body: {detail: 'Fel e-post eller lösenord.'}}))
    const {wrapper} = await render()

    await submitLogin(wrapper, 'elev', 'fel')

    expect(wrapper.find('[role="alert"]').text()).toBe('Fel e-post eller lösenord.')
  })

  it('säger att kontot är spärrat vid 403', async () => {
    mockApi(() => ({status: 403, body: {detail: 'Ditt konto är spärrat.'}}))
    const {wrapper} = await render()

    await submitLogin(wrapper, 'elev', 'elev')

    expect(wrapper.find('[role="alert"]').text()).toBe('Ditt konto är spärrat. Kontakta skolans admin.')
  })

  it('visar ett allmänt fel när inloggningen inte kan göras', async () => {
    mockApi(() => ({status: 500, body: 'boom'}))
    const {wrapper} = await render()

    await submitLogin(wrapper, 'elev', 'elev')

    expect(wrapper.find('[role="alert"]').text()).toBe('Kunde inte logga in. Försök igen.')
  })

  it('går vidare till redirect efter inloggningen', async () => {
    mockApi((_method, path) => path === '/api/auth/login'
      ? tokenResponse()
      : {status: 404})
    const {wrapper, router} = await render('/login?redirect=/groups')

    await submitLogin(wrapper, 'elev', 'elev')

    expect(router.currentRoute.value.fullPath).toBe('/groups')
  })

  it('går till startsidan om redirect är en annan värd', async () => {
    mockApi((_method, path) => path === '/api/auth/login'
      ? tokenResponse()
      : {status: 404})
    const {wrapper, router} = await render('/login?redirect=//example.com')

    await submitLogin(wrapper, 'elev', 'elev')

    expect(router.currentRoute.value.fullPath).toBe('/')
  })

  it('glömmer den förra användaren vid inloggning, så att rollen inte hänger kvar', async () => {
    const {me, status} = useCurrentUser()
    me.value = meAs('Student', ['c1'])
    status.value = 'loaded'
    mockApi(() => tokenResponse())

    const {wrapper} = await render()
    await submitLogin(wrapper, 'admin', 'admin')

    expect(me.value).toBeNull()
    expect(status.value).toBe('loading')
  })
})
