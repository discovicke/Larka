import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import {resetToasts, toastDuration, useToast} from '@/composables/useToast'

describe('useToast', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    resetToasts()
  })

  afterEach(() => {
    resetToasts()
    vi.useRealTimers()
  })

  it('visar en toast som försvinner av sig själv', () => {
    const {toasts, showToast} = useToast()

    showToast('Delen är klarmarkerad.')
    expect(toasts.value.map((toast) => toast.message)).toEqual(['Delen är klarmarkerad.'])

    vi.advanceTimersByTime(toastDuration)
    expect(toasts.value).toEqual([])
  })

  it('samma text två gånger i rad ger en toast', () => {
    const {toasts, showToast} = useToast()

    showToast('Rollen är ändrad.')
    showToast('Rollen är ändrad.')

    expect(toasts.value).toHaveLength(1)
  })

  it('går att stänga för hand', () => {
    const {toasts, showToast, dismissToast} = useToast()

    showToast('Kommentaren är skickad.')
    dismissToast(toasts.value[0]!.id)

    expect(toasts.value).toEqual([])
  })
})
