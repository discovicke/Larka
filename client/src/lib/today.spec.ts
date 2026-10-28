import {describe, expect, it} from 'vitest'

import {
  dateLine,
  deadlineText,
  dueThisWeekText,
  firstName,
  greeting,
  isUrgent,
  metaLine,
  progressPercent,
  relativeText,
  splitByWeek,
  weekdayAndTime,
} from './today'

const wednesday = new Date(2026, 9, 21, 12, 0)

function local(month: number, day: number, hours = 12, minutes = 0) {
  return new Date(2026, month - 1, day, hours, minutes).toISOString()
}

describe('today', () => {
  it('hälsar efter klockslaget', () => {
    expect(greeting(new Date(2026, 9, 21, 7))).toBe('God morgon')
    expect(greeting(new Date(2026, 9, 21, 12))).toBe('God eftermiddag')
    expect(greeting(new Date(2026, 9, 21, 17, 59))).toBe('God eftermiddag')
    expect(greeting(new Date(2026, 9, 21, 18))).toBe('God kväll')
    expect(greeting(new Date(2026, 9, 21, 2))).toBe('God kväll')
  })

  it('skriver datum och veckonummer', () => {
    expect(dateLine(wednesday)).toBe('Onsdag 21 oktober · vecka 43')
  })

  it('tar förnamnet', () => {
    expect(firstName('Christian Gennari')).toBe('Christian')
    expect(firstName(undefined)).toBe('')
  })

  it('böjer antalet som förfaller', () => {
    expect(dueThisWeekText(0)).toBe('Inget förfaller den här veckan')
    expect(dueThisWeekText(1)).toBe('1 sak förfaller den här veckan')
    expect(dueThisWeekText(3)).toBe('3 saker förfaller den här veckan')
  })

  it('delar i den här veckan och senare, med söndag kvällen i veckan', () => {
    const items = [
      {id: 'a', dueAt: local(10, 25, 23, 59)},
      {id: 'b', dueAt: local(10, 26, 0, 0)},
      {id: 'c', dueAt: local(11, 6)},
    ]

    const {thisWeek, later} = splitByWeek(items, wednesday)

    expect(thisWeek.map((item) => item.id)).toEqual(['a'])
    expect(later.map((item) => item.id)).toEqual(['b', 'c'])
  })

  it('markerar deadlines inom 48 timmar', () => {
    expect(isUrgent(local(10, 23, 11), wednesday)).toBe(true)
    expect(isUrgent(local(10, 23, 13), wednesday)).toBe(false)
  })

  it('skriver tid kvar', () => {
    expect(relativeText(local(10, 21, 12, 30), wednesday)).toBe('om 30 min')
    expect(relativeText(local(10, 21, 15), wednesday)).toBe('om 3 timmar')
    expect(relativeText(local(10, 22, 9), wednesday)).toBe('imorgon')
    expect(relativeText(local(10, 23, 23, 59), wednesday)).toBe('om 2 dagar')
    expect(relativeText(local(11, 11), wednesday)).toBe('om 3 veckor')
    expect(relativeText(local(10, 20), wednesday)).toBe('passerad')
  })

  it('skriver veckodag och tid', () => {
    expect(weekdayAndTime(local(10, 31, 23, 59))).toBe('lör 31 okt 23:59')
    expect(deadlineText(local(10, 23, 23, 59))).toBe('Fre 23 okt 23:59')
  })

  it('bygger sekundärraden', () => {
    expect(metaLine('DevOps', local(10, 25))).toBe('DevOps · sön 25 okt 12:00')
    expect(metaLine(null, local(10, 25))).toBe('sön 25 okt 12:00')
  })

  it('räknar ut andelen klara', () => {
    expect(progressPercent(9, 24)).toBe(38)
    expect(progressPercent(0, 0)).toBe(0)
  })
})
