import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'

import {useCurrentUser} from '@/composables/useCurrentUser'
import {useTheme} from '@/composables/useTheme'
import type {GitHubConnection} from '@/lib/integrations'
import {THEME_STORAGE_KEY} from '@/lib/theme'
import {mockApi, requests} from '@/test/mockApi'
import {meAs} from '@/test/me'
import SettingsView from './SettingsView.vue'

const connectedGitHub: GitHubConnection = {
  configured: true,
  appId: 5508268,
  installationId: 176124626,
  org: 'EduEdugrade-Sandbox',
  apiBaseUrl: 'https://api.github.com/',
  connected: true,
  accountLogin: 'EduEdugrade-Sandbox',
  lastVerifiedAt: '2026-10-28T10:00:00Z',
  error: null,
  missingSettings: [],
}

function settingsBackend(
  role: 'Admin' | 'Student',
  github: GitHubConnection = connectedGitHub,
  gitHubLogin: string | null = null,
  options: {muted?: string[], saveStatus?: number, groups?: {id: string, name: string}[]} = {},
) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: {...meAs(role, [], 'Anna', gitHubLogin), groups: options.groups ?? []}}
    }
    if (path === '/api/me/notification-settings') {
      return method === 'PUT'
        ? {status: options.saveStatus ?? 200, body}
        : {status: 200, body: {muted: options.muted ?? []}}
    }
    if (path === '/api/integrations/github') {
      return {status: 200, body: github}
    }
    if (method === 'POST' && path === '/api/integrations/github/connect') {
      return {status: 200, body: {...github, connected: true}}
    }
    if (method === 'DELETE' && path === '/api/integrations/github') {
      return {status: 204}
    }
    if (method === 'DELETE' && path === '/api/github/link') {
      return {status: 204}
    }
    if (path.startsWith('/api/github/authorize')) {
      return {status: 200, body: {url: 'https://github.com/login/oauth/authorize?test=1'}}
    }
    return {status: 404}
  })
}

describe('SettingsView', () => {
  beforeEach(() => {
    useCurrentUser().resetCurrentUser()
    useTheme().preference.value = 'auto'
    localStorage.removeItem(THEME_STORAGE_KEY)
    document.documentElement.classList.remove('dark')
    settingsBackend('Student')
  })

  it('byter tema direkt och sparar valet', async () => {
    const wrapper = mount(SettingsView)

    expect(wrapper.findAll('[role="radio"]')).toHaveLength(3)

    await wrapper.find('[data-test="theme-dark"]').trigger('click')
    await flushPromises()

    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark')
    expect(wrapper.find('[data-test="resolved-theme"]').text()).toBe('mörkt tema')

    await wrapper.find('[data-test="theme-light"]').trigger('click')
    await flushPromises()

    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
    expect(wrapper.find('[data-test="resolved-theme"]').text()).toBe('ljust tema')
  })

  it('visar GitHub-anslutningen för admin', async () => {
    settingsBackend('Admin')

    const wrapper = mount(SettingsView)
    await flushPromises()
    await flushPromises()

    expect(wrapper.find('[data-test="integrations"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="github-status"]').text()).toBe('Ansluten')
    expect(wrapper.find('[data-test="github-org"]').text()).toBe('EduEdugrade-Sandbox')
    expect(wrapper.find('[data-test="github-account"]').text()).toBe('EduEdugrade-Sandbox')
    expect(wrapper.find('[data-test="github-app-id"]').text()).toBe('5508268')
    expect(wrapper.find('[data-test="github-installation-id"]').text()).toBe('176124626')
    expect(wrapper.find('[data-test="github-verified"]').text()).toContain('28 okt')
    expect(wrapper.find('[data-test="github-error"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="github-disconnect"]').exists()).toBe(true)
  })

  it('ansluter GitHub för admin när den är konfigurerad men inte ansluten', async () => {
    settingsBackend('Admin', {
      ...connectedGitHub,
      connected: false,
    })

    const wrapper = mount(SettingsView)
    await flushPromises()
    await flushPromises()

    expect(wrapper.find('[data-test="github-status"]').text()).toBe('Inte ansluten')
    const connectButton = wrapper.find('[data-test="github-connect"]')
    expect(connectButton.exists()).toBe(true)

    await connectButton.trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="github-status"]').text()).toBe('Ansluten')
  })

  it('kopplar från GitHub för admin', async () => {
    settingsBackend('Admin')

    const wrapper = mount(SettingsView)
    await flushPromises()
    await flushPromises()

    const disconnectButton = wrapper.find('[data-test="github-disconnect"]')
    expect(disconnectButton.exists()).toBe(true)

    await disconnectButton.trigger('click')
    await flushPromises()
  })

  it('visar saknad konfiguration för admin', async () => {
    settingsBackend('Admin', {
      ...connectedGitHub,
      configured: false,
      appId: 0,
      installationId: 0,
      org: '',
      connected: false,
      accountLogin: null,
      lastVerifiedAt: null,
      missingSettings: ['AppId', 'InstallationId', 'Org', 'PrivateKey'],
    })

    const wrapper = mount(SettingsView)
    await flushPromises()
    await flushPromises()

    expect(wrapper.find('[data-test="github-status"]').text()).toBe('Ej konfigurerad')
    expect(wrapper.find('[data-test="github-missing"]').text()).toContain('App ID')
    expect(wrapper.find('[data-test="github-missing"]').text()).toContain('Privat nyckel')
  })

  it('visar användarens GitHub-konto som inte kopplad för elev', async () => {
    const wrapper = mount(SettingsView)
    await flushPromises()

    expect(wrapper.find('[data-test="integrations"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="user-github"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="user-github-status"]').text()).toBe('Inte kopplad')
    expect(wrapper.find('[data-test="user-github-connect"]').exists()).toBe(true)
  })

  it('visar användarens GitHub-konto som kopplad med frånkopplingsknapp', async () => {
    settingsBackend('Student', connectedGitHub, 'elev-kalle')

    const wrapper = mount(SettingsView)
    await flushPromises()

    expect(wrapper.find('[data-test="user-github-status"]').text()).toBe('Kopplad')
    expect(wrapper.find('[data-test="user-github-login"]').text()).toBe('@elev-kalle')
    const unlinkButton = wrapper.find('[data-test="user-github-disconnect"]')
    expect(unlinkButton.exists()).toBe(true)

    await unlinkButton.trigger('click')
    await flushPromises()
  })

  it('visar profilen med roll, skola, e-post och grupper', async () => {
    settingsBackend('Student', connectedGitHub, null, {
      groups: [{id: 'g1', name: 'NET25'}, {id: 'g2', name: 'Lag 1'}],
    })

    const wrapper = mount(SettingsView)
    await flushPromises()

    expect(wrapper.find('[data-test="profile-name"]').text()).toBe('Anna')
    expect(wrapper.find('[data-test="profile-line"]').text()).toBe('Elev · Edugrade · anna@example.com')
    expect(wrapper.find('[data-test="profile-groups"]').text()).toContain('NET25')
    expect(wrapper.find('[data-test="profile-groups"]').text()).toContain('Lag 1')
  })

  it('säger att användaren saknar grupper', async () => {
    const wrapper = mount(SettingsView)
    await flushPromises()

    expect(wrapper.find('[data-test="profile-no-groups"]').text()).toBe('Inga grupper')
  })

  it('visar aviseringarna som valts bort som avbockade', async () => {
    settingsBackend('Student', connectedGitHub, null, {muted: ['NewsPublished', 'SubmissionGraded']})

    const wrapper = mount(SettingsView)
    await flushPromises()

    const state = (id: string) => wrapper.find(`[data-test="notify-${id}"]`).attributes('aria-checked')
    expect(state('news')).toBe('false')
    expect(state('assignments')).toBe('true')
    expect(state('deadline')).toBe('true')
    // Betyg och återkoppling styr två slag: räcker att ett är avstängt för att rutan ska vara tom.
    expect(state('feedback')).toBe('false')
  })

  it('sparar direkt när Nyheter bockas av', async () => {
    const wrapper = mount(SettingsView)
    await flushPromises()

    await wrapper.find('[data-test="notify-news"]').trigger('click')
    await flushPromises()

    const put = requests.find((request) => request.method === 'PUT')
    expect(put?.path).toBe('/api/me/notification-settings')
    expect(put?.body).toEqual({muted: ['NewsPublished']})
    expect(wrapper.find('[data-test="notify-news"]').attributes('aria-checked')).toBe('false')
  })

  it('stänger av båda slagen för betyg och återkoppling med en ruta', async () => {
    const wrapper = mount(SettingsView)
    await flushPromises()

    await wrapper.find('[data-test="notify-feedback"]').trigger('click')
    await flushPromises()

    const put = requests.find((request) => request.method === 'PUT')
    expect(put?.body).toEqual({muted: ['SubmissionGraded', 'SubmissionNeedsWork']})
  })

  it('återställer rutan och visar felet när sparandet misslyckas', async () => {
    settingsBackend('Student', connectedGitHub, null, {saveStatus: 500})

    const wrapper = mount(SettingsView)
    await flushPromises()
    await wrapper.find('[data-test="notify-news"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="notify-news"]').attributes('aria-checked')).toBe('true')
    expect(wrapper.find('[data-test="notifications-error"]').exists()).toBe(true)
  })

  it('visar Integrationer i menyn bara för admin', async () => {
    const student = mount(SettingsView)
    await flushPromises()
    expect(student.find('nav[aria-label="Inställningar"]').text()).not.toContain('Integrationer')
    expect(student.find('nav[aria-label="Inställningar"]').text()).toContain('Kopplade konton')

    useCurrentUser().resetCurrentUser()
    settingsBackend('Admin')
    const admin = mount(SettingsView)
    await flushPromises()
    await flushPromises()
    expect(admin.find('nav[aria-label="Inställningar"]').text()).toContain('Integrationer')
  })
})
