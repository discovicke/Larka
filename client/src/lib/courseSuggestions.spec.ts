import {describe, expect, it} from 'vitest'

import {addDays, courseLengthDays, suggestCourseCode, suggestCourseTitle} from './courseSuggestions'

describe('suggestCourseTitle', () => {
  it('lägger klassen efter källkursens titel så att kopian får ett eget namn', () => {
    expect(suggestCourseTitle('NET27', 'DevOps')).toBe('DevOps – NET27')
  })

  it('låter titeln vara när den redan nämner klassen', () => {
    expect(suggestCourseTitle('NET27', 'DevOps NET27')).toBe('DevOps NET27')
  })
})

describe('suggestCourseCode', () => {
  it('byter klassen i koden och behåller kursens del', () => {
    expect(suggestCourseCode('NET27', '.NET25_DEVOPS')).toBe('NET27_DEVOPS')
  })

  it('använder hela koden när den saknar understreck', () => {
    expect(suggestCourseCode('NET27', 'DEVOPS25')).toBe('NET27_DEVOPS25')
  })

  it('ersätter mellanslag i klassens namn', () => {
    expect(suggestCourseCode('Klass 27', 'A_B')).toBe('Klass_27_B')
  })
})

describe('kursens längd', () => {
  it('räknar dagarna mellan två datum', () => {
    expect(courseLengthDays('2026-10-12', '2026-11-13')).toBe(32)
  })

  it('saknar längd när ett datum saknas', () => {
    expect(courseLengthDays('2026-10-12', null)).toBeNull()
  })

  it('lägger till dagar över en månadsgräns', () => {
    expect(addDays('2026-11-16', 32)).toBe('2026-12-18')
  })
})
