import {describe, expect, it} from 'vitest'

import {
  abbreviateCourseTitle,
  addDays,
  courseLengthDays,
  suggestCourseCode,
  suggestCourseTitle,
  suggestNewCourseCode,
  termCode,
} from './courseSuggestions'

describe('abbreviateCourseTitle', () => {
  it.each([
    ['DevOps', 'DevOps'],
    ['LIA', 'LIA'],
    ['Databaser', 'Data'],
    ['Examensarbete', 'Exam'],
    ['Designmönster & arkitektur', 'DeAr'],
    ['Nätuppbyggnad i praktiken', 'NaPr'],
    ['Objektorienterad Programmering med C# & .NET', 'OPCN'],
    ['Projektledning & Agila metoder', 'PAM'],
    ['  ', ''],
  ])('förkortar "%s" till "%s"', (title, expected) => {
    expect(abbreviateCourseTitle(title)).toBe(expected)
  })
})

describe('suggestNewCourseCode', () => {
  it('sätter klassens kod före förkortningen', () => {
    expect(suggestNewCourseCode('.NET30', 'Databaser', [])).toBe('.NET30_Data')
  })

  it('lägger till en siffra när koden redan finns', () => {
    expect(suggestNewCourseCode('.NET30', 'Databaser', ['.NET30_Data', '.net30_data2'])).toBe('.NET30_Data3')
  })

  it('föreslår ingenting utan namn', () => {
    expect(suggestNewCourseCode('.NET30', '', [])).toBe('')
  })
})

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
    expect(suggestCourseCode('.NET27', '.NET25_DevOps')).toBe('.NET27_DevOps')
  })

  it('använder hela koden när den saknar understreck', () => {
    expect(suggestCourseCode('NET27', 'DEVOPS25')).toBe('NET27_DEVOPS25')
  })

  it('ersätter mellanslag i klassens namn', () => {
    expect(suggestCourseCode('Klass 27', 'A_B')).toBe('Klass_27_B')
  })

  it('lägger till terminen när kursen går igen i samma klass', () => {
    expect(suggestCourseCode('TE24A', 'TE24A_Webb1', ['TE24A_Webb1'], '2026-08-17')).toBe('TE24A_Webb1_HT26')
    expect(suggestCourseCode('TE24A', 'TE24A_Webb1', ['te24a_webb1'], '2027-01-11')).toBe('TE24A_Webb1_VT27')
  })

  it('lägger till ett löpnummer utan startdatum eller när terminen också är tagen', () => {
    expect(suggestCourseCode('TE24A', 'TE24A_Webb1', ['TE24A_Webb1'])).toBe('TE24A_Webb1_2')
    expect(suggestCourseCode('TE24A', 'TE24A_Webb1', ['TE24A_Webb1', 'TE24A_Webb1_2'])).toBe('TE24A_Webb1_3')
    expect(suggestCourseCode('TE24A', 'TE24A_Webb1', ['TE24A_Webb1', 'TE24A_Webb1_HT26'], '2026-08-17'))
      .toBe('TE24A_Webb1_HT26_2')
  })

  it('utgår från kursdelen när källan själv har en termin eller ett löpnummer', () => {
    expect(suggestCourseCode('TE25A', 'TE24A_Webb1_HT26', ['TE24A_Webb1_HT26'])).toBe('TE25A_Webb1')
    expect(suggestCourseCode('TE24A', 'TE24A_Webb1_2', ['TE24A_Webb1', 'TE24A_Webb1_2'], '2027-01-11'))
      .toBe('TE24A_Webb1_VT27')
  })
})

describe('termCode', () => {
  it('räknar juli till december som hösttermin och resten som vårtermin', () => {
    expect(termCode('2026-07-01')).toBe('HT26')
    expect(termCode('2026-12-31')).toBe('HT26')
    expect(termCode('2027-06-30')).toBe('VT27')
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
