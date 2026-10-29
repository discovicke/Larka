import {describe, expect, it} from 'vitest'

import {courseStatus} from '@/lib/courseStatus'

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
