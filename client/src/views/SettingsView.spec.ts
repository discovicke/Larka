import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'

import {useTheme} from '@/composables/useTheme'
import {THEME_STORAGE_KEY} from '@/lib/theme'
import SettingsView from './SettingsView.vue'

describe('SettingsView', () => {
  beforeEach(() => {
    useTheme().preference.value = 'auto'
    localStorage.removeItem(THEME_STORAGE_KEY)
    document.documentElement.classList.remove('dark')
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
})
