import {describe, expect, it} from 'vitest'

import {courseStatus, isFinished, isGroupFinished, splitFinished} from '@/lib/courseStatus'

const today = new Date(2026, 9, 29, 15, 30)

describe('courseStatus', () => {
  it('en kurs som startar idag pågår', () => {
    expect(courseStatus({startsOn: '2026-10-29', endsOn: '2026-11-30'}, today)).toBe('running')
  })

  it('en kurs som slutar idag pågår fortfarande', () => {
    expect(courseStatus({startsOn: '2026-09-01', endsOn: '2026-10-29'}, today)).toBe('running')
  })

  it('dagen efter slutdatumet är kursen avslutad', () => {
    expect(courseStatus({startsOn: '2026-09-01', endsOn: '2026-10-28'}, today)).toBe('finished')
  })

  it('en kurs som startar i morgon är kommande', () => {
    expect(courseStatus({startsOn: '2026-10-30', endsOn: '2026-12-01'}, today)).toBe('upcoming')
  })

  it('utan datum finns inget läge', () => {
    expect(courseStatus({}, today)).toBeNull()
    expect(courseStatus({startsOn: null, endsOn: null}, today)).toBeNull()
  })

  it('med bara ett datum avgörs läget av det', () => {
    expect(courseStatus({startsOn: '2026-10-01'}, today)).toBe('running')
    expect(courseStatus({startsOn: '2026-11-16'}, today)).toBe('upcoming')
    expect(courseStatus({endsOn: '2026-11-16'}, today)).toBe('running')
    expect(courseStatus({endsOn: '2026-10-01'}, today)).toBe('finished')
  })
})

describe('isFinished', () => {
  it('är sant först dagen efter slutdatumet', () => {
    expect(isFinished({endsOn: '2026-10-28'}, today)).toBe(true)
    expect(isFinished({endsOn: '2026-10-29'}, today)).toBe(false)
  })

  it('kurser utan datum och kommande kurser är inte avslutade', () => {
    expect(isFinished({}, today)).toBe(false)
    expect(isFinished({startsOn: '2026-11-01', endsOn: '2026-12-01'}, today)).toBe(false)
  })
})

describe('splitFinished', () => {
  it('delar kurserna och behåller ordningen', () => {
    const courses = [
      {id: 'a', endsOn: '2026-01-01'},
      {id: 'b', endsOn: '2026-12-01'},
      {id: 'c'},
      {id: 'd', endsOn: '2026-02-01'},
    ]
    const {current, finished} = splitFinished(courses, today)
    expect(current.map((course) => course.id)).toEqual(['b', 'c'])
    expect(finished.map((course) => course.id)).toEqual(['a', 'd'])
  })
})

describe('isGroupFinished', () => {
  it('en klass är avslutad när den sista kursen är det', () => {
    expect(isGroupFinished({lastCourseEndsOn: '2026-06-01'}, today)).toBe(true)
    expect(isGroupFinished({lastCourseEndsOn: '2026-11-30'}, today)).toBe(false)
  })

  it('en klass utan kurser, eller med en kurs utan slutdatum, är inte avslutad', () => {
    expect(isGroupFinished({lastCourseEndsOn: null}, today)).toBe(false)
    expect(isGroupFinished({}, today)).toBe(false)
  })
})
