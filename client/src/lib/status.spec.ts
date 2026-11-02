import {describe, expect, it} from 'vitest'

import {
  courseStatusKey,
  deadlineStatusKey,
  partStatusKey,
  statuses,
  type StatusKey,
  statusVariant,
  submissionStatusKey,
  toneMeanings,
  toneVariants,
} from '@/lib/status'

const now = new Date('2026-10-30T12:00:00Z')
const hours = (count: number) => new Date(now.getTime() + count * 3_600_000).toISOString()

describe('statuses', () => {
  it('varje läge har en text och en känd ton', () => {
    for (const definition of Object.values(statuses)) {
      expect(definition.label).not.toBe('')
      expect(Object.keys(toneMeanings)).toContain(definition.tone)
    }
  })

  it('varje ton har en egen badge-variant', () => {
    const variants = Object.values(toneVariants)
    expect(new Set(variants).size).toBe(variants.length)
  })

  it.each<[StatusKey, string, string]>([
    ['course.upcoming', 'Kommande', 'neutral'],
    ['course.running', 'Pågår', 'neutral'],
    ['course.finished', 'Avslutad', 'neutral'],
    ['course.noTeacher', 'Ingen lärare', 'warning'],
    ['publishing.draft', 'Utkast', 'neutral'],
    ['publishing.published', 'Publicerad', 'neutral'],
    ['publishing.scheduled', 'Schemalagd', 'info'],
    ['progress.todo', 'Att göra', 'neutral'],
    ['progress.locked', 'Låst', 'neutral'],
    ['progress.done', 'Klar', 'success'],
    ['submission.submitted', 'Inlämnad', 'info'],
    ['submission.needsWork', 'Komplettera', 'destructive'],
    ['submission.missed', 'Ej inlämnat', 'destructive'],
    ['deadline.soon', 'Deadline snart', 'warning'],
    ['deadline.overdue', 'Försenad', 'destructive'],
  ])('%s heter %s och har tonen %s', (key, label, tone) => {
    expect(statuses[key].label).toBe(label)
    expect(statuses[key].tone).toBe(tone)
  })

  it('bara Klar är grön', () => {
    const green = Object.entries(statuses).filter(([, definition]) => definition.tone === 'success')
    expect(green.map(([key]) => key)).toEqual(['progress.done'])
  })

  it('statusVariant ger tonens variant', () => {
    expect(statusVariant('progress.done')).toBe('success')
    expect(statusVariant('course.running')).toBe('outline')
    expect(statusVariant('deadline.overdue')).toBe('destructive')
  })
})

describe('courseStatusKey', () => {
  it('översätter kursens läge', () => {
    expect(courseStatusKey('running')).toBe('course.running')
    expect(courseStatusKey('finished')).toBe('course.finished')
  })
})

describe('submissionStatusKey', () => {
  it.each([
    ['Submitted', 'submission.submitted'],
    ['NeedsWork', 'submission.needsWork'],
    ['Graded', 'submission.graded'],
    ['NotSubmitted', 'submission.missed'],
    ['NotStarted', 'submission.notSubmitted'],
    [null, 'submission.notSubmitted'],
    [undefined, 'submission.notSubmitted'],
  ] as const)('%s ger %s', (status, key) => {
    expect(submissionStatusKey(status)).toBe(key)
  })
})

describe('deadlineStatusKey', () => {
  it('är försenad när deadlinen har passerat', () => {
    expect(deadlineStatusKey(hours(-1), false, now)).toBe('deadline.overdue')
  })

  it('är snart inom två dygn', () => {
    expect(deadlineStatusKey(hours(47), false, now)).toBe('deadline.soon')
  })

  it('är ingenting längre fram', () => {
    expect(deadlineStatusKey(hours(72), false, now)).toBeNull()
  })

  it('är ingenting utan deadline eller när delen är klar', () => {
    expect(deadlineStatusKey(null, false, now)).toBeNull()
    expect(deadlineStatusKey(hours(-5), true, now)).toBeNull()
  })
})

describe('partStatusKey', () => {
  const base = {
    type: 'Assignment',
    isLocked: false,
    isCompleted: false,
    submissionStatus: null,
    gradeLevel: null,
  } as const
  const part = (overrides: Partial<Parameters<typeof partStatusKey>[0]>) => partStatusKey({...base, ...overrides})

  it('en inlämnad men obedömd uppgift är Inlämnad, även när isCompleted är sann', () => {
    expect(part({isCompleted: true, submissionStatus: 'Submitted'})).toBe('submission.submitted')
  })

  it('en uppgift som läraren markerat som ej inlämnad är Ej inlämnat', () => {
    expect(part({isCompleted: false, submissionStatus: 'NotSubmitted'})).toBe('submission.missed')
  })

  it('en uppgift som ska kompletteras är Komplettera', () => {
    expect(part({isCompleted: false, submissionStatus: 'NeedsWork'})).toBe('submission.needsWork')
  })

  it.each(['Pass', 'Distinction', null] as const)('en bedömd uppgift med betyget %s är Klar', (gradeLevel) => {
    expect(part({isCompleted: true, submissionStatus: 'Graded', gradeLevel})).toBe('progress.done')
  })

  it('en bedömd uppgift med IG är Bedömd', () => {
    expect(part({isCompleted: true, submissionStatus: 'Graded', gradeLevel: 'Fail'})).toBe('submission.graded')
  })

  it('en klarmarkerad del är Klar och en annan Att göra', () => {
    expect(part({type: 'Theory', isCompleted: true})).toBe('progress.done')
    expect(part({type: 'Theory'})).toBe('progress.todo')
    expect(part({})).toBe('progress.todo')
  })

  it('en låst del är Låst', () => {
    expect(part({isLocked: true})).toBe('progress.locked')
  })
})
