import {describe, expect, it} from 'vitest'

import {timelineDate, timelineProgress} from '@/lib/timeline'

describe('timeline', () => {
  it('räknar ut hur stor del av kursen som passerat', () => {
    const progress = timelineProgress('2026-01-01', '2026-01-31', new Date(2026, 0, 16, 15))

    expect(progress.percent).toBe(50)
    expect(progress.daysLeft).toBe(15)
    expect(progress.nearEnd).toBe(true)
    expect(progress.hasDates).toBe(true)
  })

  it('är tom före kursstart och full efter kursens slut', () => {
    expect(timelineProgress('2026-02-01', '2026-02-28', new Date(2026, 0, 20)).percent).toBe(0)
    expect(timelineProgress('2026-02-01', '2026-02-28', new Date(2026, 2, 1)).percent).toBe(100)
  })

  it('varnar bara när kursen har datum och är nära slutet', () => {
    expect(timelineProgress('2026-01-01', '2026-02-28', new Date(2026, 0, 20)).nearEnd)
      .toBe(false)
    expect(timelineProgress('2026-01-01', '2026-02-28', new Date(2026, 1, 10)).nearEnd)
      .toBe(true)
    expect(timelineProgress(null, '2026-02-28', new Date(2026, 1, 10))).toEqual({
      percent: 0,
      daysLeft: null,
      nearEnd: false,
      hasDates: false,
    })
  })

  it('formaterar datum med år', () => {
    expect(timelineDate('2026-01-12')).toBe('12 jan 2026')
    expect(timelineDate(null)).toBe('Datum saknas')
  })
})
