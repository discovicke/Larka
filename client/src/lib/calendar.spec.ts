import {describe, expect, it} from 'vitest'

import {
  addDays,
  dateKey,
  groupByDate,
  monthGrid,
  monthHeading,
  startOfWeek,
  weekDays,
  weekHeading,
  weekLabel,
  weekNumber,
} from './calendar'

describe('calendar', () => {
  it('börjar veckan på måndag', () => {
    expect(dateKey(startOfWeek(new Date(2026, 9, 21)))).toBe('2026-10-19')
    expect(dateKey(startOfWeek(new Date(2026, 9, 19)))).toBe('2026-10-19')
    expect(dateKey(startOfWeek(new Date(2026, 9, 25)))).toBe('2026-10-19')
  })

  it('räknar svenskt veckonummer', () => {
    expect(weekNumber(new Date(2026, 9, 21))).toBe(43)
    expect(weekNumber(new Date(2026, 0, 1))).toBe(1)
    expect(weekNumber(new Date(2021, 0, 3))).toBe(53)
    expect(weekNumber(new Date(2024, 11, 30))).toBe(1)
  })

  it('ger sju dagar i veckan', () => {
    const days = weekDays(new Date(2026, 9, 19)).map(dateKey)
    expect(days).toEqual([
      '2026-10-19', '2026-10-20', '2026-10-21', '2026-10-22', '2026-10-23', '2026-10-24', '2026-10-25',
    ])
  })

  it('ger ett månadsrutnät om 6 x 7 med måndag först', () => {
    const grid = monthGrid(new Date(2026, 9, 15))
    expect(grid).toHaveLength(42)
    expect(dateKey(grid[0])).toBe('2026-09-28')
    expect(dateKey(grid[41])).toBe('2026-11-08')
    expect(grid[0].getDay()).toBe(1)
  })

  it('delar in raderna per lokalt datum i tidsordning', () => {
    const late = {at: new Date(2026, 9, 23, 23, 59).toISOString(), id: 'late'}
    const early = {at: new Date(2026, 9, 23, 8, 0).toISOString(), id: 'early'}
    const other = {at: new Date(2026, 9, 24, 12, 0).toISOString(), id: 'other'}

    const groups = groupByDate([late, other, early])

    expect(groups.get('2026-10-23')?.map((item) => item.id)).toEqual(['early', 'late'])
    expect(groups.get('2026-10-24')?.map((item) => item.id)).toEqual(['other'])
  })

  it('skriver rubriker på svenska', () => {
    expect(weekHeading(new Date(2026, 9, 19))).toBe('19–25 oktober 2026')
    expect(weekHeading(new Date(2026, 9, 26))).toBe('26 oktober – 1 november 2026')
    expect(weekHeading(new Date(2026, 11, 28))).toBe('28 december 2026 – 3 januari 2027')
    expect(monthHeading(new Date(2026, 9, 1))).toBe('oktober 2026')
    expect(weekLabel(new Date(2026, 9, 21))).toBe('vecka 43')
  })

  it('lägger på dagar över månadsskiftet', () => {
    expect(dateKey(addDays(new Date(2026, 9, 30), 3))).toBe('2026-11-02')
  })
})
