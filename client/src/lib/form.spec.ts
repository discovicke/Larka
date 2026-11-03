import {describe, expect, it} from 'vitest'

import {earliestPersonalDate} from './form'

describe('earliestPersonalDate', () => {
  const now = new Date('2026-11-10T12:00:00')

  it('ger dagen för klassens sista deadline när den ligger framåt', () => {
    expect(earliestPersonalDate('2026-11-25T12:00:00', now)).toBe('2026-11-25')
  })

  it('ger idag när klassens sista deadline har passerat eller saknas', () => {
    expect(earliestPersonalDate('2026-11-01T12:00:00', now)).toBe('2026-11-10')
    expect(earliestPersonalDate(null, now)).toBe('2026-11-10')
    expect(earliestPersonalDate(undefined, now)).toBe('2026-11-10')
  })
})
