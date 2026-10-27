import {describe, expect, it} from 'vitest'

import {applyTheme, isThemePreference, resolveTheme} from './theme'

describe('theme', () => {
  it('känner igen sparade temavalen', () => {
    expect(isThemePreference('auto')).toBe(true)
    expect(isThemePreference('light')).toBe(true)
    expect(isThemePreference('dark')).toBe(true)
    expect(isThemePreference('system')).toBe(false)
    expect(isThemePreference(null)).toBe(false)
  })

  it('följer systemet i automatiskt läge', () => {
    expect(resolveTheme('auto', true)).toBe('dark')
    expect(resolveTheme('auto', false)).toBe('light')
  })

  it('låter ett manuellt val gå före systemet', () => {
    expect(resolveTheme('light', true)).toBe('light')
    expect(resolveTheme('dark', false)).toBe('dark')
  })

  it('sätter dark-klassen och rätt color-scheme', () => {
    const root = document.createElement('div')

    applyTheme('dark', root)
    expect(root.classList.contains('dark')).toBe(true)
    expect(root.style.colorScheme).toBe('dark')

    applyTheme('light', root)
    expect(root.classList.contains('dark')).toBe(false)
    expect(root.style.colorScheme).toBe('light')
  })
})
