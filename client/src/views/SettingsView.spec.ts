import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'

import {useCurrentUser} from '@/composables/useCurrentUser'
import {useTheme} from '@/composables/useTheme'
import type {GitHubConnection} from '@/lib/integrations'
import {THEME_STORAGE_KEY} from '@/lib/theme'
import {mockApi} from '@/test/mockApi'
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

function settingsBackend(role: 'Admin' | 'Student', github = connectedGitHub) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs(role)}
    }
    if (path === '/api/integrations/github') {
      return {status: 200, body: github}
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

  it('visar inte GitHub-anslutningen för elev', async () => {
    const wrapper = mount(SettingsView)
    await flushPromises()

    expect(wrapper.find('[data-test="integrations"]').exists()).toBe(false)
  })
})
