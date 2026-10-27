import {usePreferredDark, useStorage} from '@vueuse/core'
import {computed, watch} from 'vue'

import {
  applyTheme,
  isThemePreference,
  resolveTheme,
  THEME_STORAGE_KEY,
  type ResolvedTheme,
  type ThemePreference,
} from '@/lib/theme'

const stored = useStorage<string>(THEME_STORAGE_KEY, 'auto')
const systemPrefersDark = usePreferredDark()

const preference = computed<ThemePreference>({
  get: () => isThemePreference(stored.value)
    ? stored.value
    : 'auto',
  set: (value) => {
    stored.value = value
  },
})

const resolvedTheme = computed<ResolvedTheme>(() =>
  resolveTheme(preference.value, systemPrefersDark.value))

let initialized = false

/** Sätter temat innan appen monteras, så att rätt färger används direkt. */
export function initializeTheme(): void {
  if (initialized) {
    return
  }
  initialized = true
  watch(resolvedTheme, (theme) => applyTheme(theme), {immediate: true})
}

export function useTheme() {
  initializeTheme()
  return {preference, resolvedTheme}
}
