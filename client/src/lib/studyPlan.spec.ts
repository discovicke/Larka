import {describe, expect, it} from 'vitest'

import {assignmentStatus, coursePeriod, courseStatusLabel, gradeText, shortDate} from '@/lib/studyPlan'

const now = new Date('2026-10-27T12:00:00Z')

function assignment(
  status: 'NotStarted' | 'Submitted' | 'NeedsWork' | 'Graded',
  dueAt: string | null = null,
  resubmitDueAt: string[] = [],
) {
  return {
    linkId: 'l1',
    title: 'Laboration',
    dueAt,
    resubmitDueAt,
    personalDueAt: null,
    status,
    grade: status === 'Graded'
      ? 'Pass' as const
      : null,
    feedback: null,
    gradedAt: null,
  }
}

describe('courseStatusLabel', () => {
  it('översätter kursens status till svenska', () => {
    expect(courseStatusLabel('Completed')).toBe('Klar')
    expect(courseStatusLabel('Ongoing')).toBe('Pågår')
    expect(courseStatusLabel('Upcoming')).toBe('Kommande')
  })
})

describe('gradeText', () => {
  it('visar betygsbokstaven och Ej satt', () => {
    expect(gradeText('Fail')).toBe('Icke godkänd')
    expect(gradeText('Pass')).toBe('Godkänd')
    expect(gradeText('Distinction')).toBe('Väl godkänd')
    expect(gradeText(null)).toBe('Ej satt')
  })
})

describe('coursePeriod', () => {
  it('visar månad och år, med årtalet en gång när det är samma', () => {
    expect(coursePeriod({startsOn: '2026-10-01', endsOn: '2026-11-13'})).toBe('okt – nov 2026')
    expect(coursePeriod({startsOn: '2026-10-01', endsOn: '2026-10-31'})).toBe('okt 2026')
  })

  it('visar båda årtalen när perioden går över ett årsskifte', () => {
    expect(coursePeriod({startsOn: '2025-11-01', endsOn: '2026-01-31'})).toBe('nov 2025 – jan 2026')
  })

  it('klarar en period med bara start eller slut', () => {
    expect(coursePeriod({startsOn: '2026-11-01', endsOn: null})).toBe('nov 2026 –')
    expect(coursePeriod({startsOn: null, endsOn: '2026-11-30'})).toBe('– nov 2026')
    expect(coursePeriod({startsOn: null, endsOn: null})).toBe('–')
  })
})

describe('shortDate', () => {
  it('visar dag och månad utan veckodag', () => {
    expect(shortDate('2026-10-23')).toBe('23 okt')
    expect(shortDate('2026-11-06T16:00:00Z')).toBe('6 nov')
  })
})

describe('assignmentStatus', () => {
  it('visar pågår med deadline, slutade och ej inlämnad', () => {
    expect(assignmentStatus(assignment('NotStarted', '2026-11-06T16:00:00Z'), now))
      .toBe('Pågår · deadline 6 nov')
    expect(assignmentStatus(assignment('NotStarted', '2026-10-23T16:00:00Z', ['2026-12-01T16:00:00Z']), now))
      .toBe('Deadline passerade 23 okt')
    expect(assignmentStatus(assignment('NotStarted'), now)).toBe('Inte inlämnad')
  })

  it('visar inlämnad, komplettering och betyg', () => {
    expect(assignmentStatus(assignment('Submitted'), now)).toBe('Inlämnad')
    expect(assignmentStatus(assignment('NeedsWork'), now)).toBe('Komplettera')
    expect(assignmentStatus(assignment('Graded'), now)).toBe('Godkänd')
    expect(assignmentStatus(assignment('NotStarted', '2020-01-01T10:00:00Z'), now)).toBe('IG · Ingen inlämning')
    expect(assignmentStatus(assignment('NeedsWork', '2020-01-01T10:00:00Z'), now))
      .toBe('IG · Kompletteringen lämnades inte in')
  })
})
