import {describe, expect, it} from 'vitest'

import {deadlineLine, type GradingRow, groupByAssignment, gradingTabOf, groupRowsByTab, nextGradingRow} from '@/lib/gradingQueue'

function row(linkId: string, name: string, extra: Partial<GradingRow> = {}, status = 'Submitted'): GradingRow {
  return {
    submission: {id: `${linkId}-${name}`, status} as GradingRow['submission'],
    courseId: 'c1',
    courseTitle: 'DevOps',
    linkId,
    title: linkId,
    name,
    dueAt: null,
    resubmitDueAt: [],
    gradeScale: 'PassFailDistinction',
    previousGrade: false,
    ...extra,
  }
}

const now = new Date('2026-11-03T12:00:00Z')
const past = '2026-11-01T12:00:00Z'
const later = '2026-11-10T12:00:00Z'

describe('nextGradingRow', () => {
  it('väljer första namnet i nästa uppgift efter den aktuella', () => {
    const rows = [row('a', 'Ada'), row('b', 'Östen'), row('b', 'Bo'), row('c', 'Cia')]

    expect(nextGradingRow(rows, 'a')?.name).toBe('Bo')
  })

  it('börjar om från början när inget väntar efter den aktuella', () => {
    const rows = [row('a', 'Ada'), row('b', 'Bo')]

    expect(nextGradingRow(rows, 'b')?.name).toBe('Ada')
  })

  it('är null när bara den aktuella uppgiften har obedömt arbete', () => {
    expect(nextGradingRow([row('a', 'Ada')], 'a')).toBeNull()
    expect(nextGradingRow([], 'a')).toBeNull()
  })
})

describe('gradingTabOf', () => {
  it('lägger inlämnat i Inlämnat', () => {
    expect(gradingTabOf(row('a', 'Ada'), now)).toBe('submitted')
  })

  it('lägger inte påbörjad i Inte inlämnat först när huvuddeadlinen har passerat', () => {
    expect(gradingTabOf(row('a', 'Ada', {dueAt: past}, 'NotStarted'), now)).toBe('missed')
    expect(gradingTabOf(row('a', 'Ada', {dueAt: later}, 'NotStarted'), now)).toBeNull()
    expect(gradingTabOf(row('a', 'Ada', {}, 'NotStarted'), now)).toBeNull()
  })

  it('skiljer Chanserna slut från Väntar på eleven på om en deadline återstår', () => {
    const open = {dueAt: past, resubmitDueAt: [later]}
    const closed = {dueAt: past, resubmitDueAt: [past]}
    expect(gradingTabOf(row('a', 'Ada', open, 'NeedsWork'), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', open, 'NotSubmitted'), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', closed, 'NeedsWork'), now)).toBe('out')
    expect(gradingTabOf(row('a', 'Ada', closed, 'NotSubmitted'), now)).toBe('out')
  })

  it('räknar rader per flik', () => {
    const tabs = groupRowsByTab([
      row('a', 'Ada'),
      row('a', 'Bo', {dueAt: past}, 'NotStarted'),
      row('a', 'Cia', {dueAt: past}, 'NeedsWork'),
    ], now)
    expect(tabs.submitted).toHaveLength(1)
    expect(tabs.missed).toHaveLength(1)
    expect(tabs.out).toHaveLength(1)
    expect(tabs.waiting).toHaveLength(0)
  })
})

describe('groupByAssignment', () => {
  it('grupperar per uppgift i första radens ordning och sorterar namnen', () => {
    const groups = groupByAssignment([row('b', 'Östen'), row('a', 'Cia'), row('b', 'Bo')])

    expect(groups.map((group) => group.linkId)).toEqual(['b', 'a'])
    expect(groups[0].rows.map((item) => item.name)).toEqual(['Bo', 'Östen'])
  })
})

describe('deadlineLine', () => {
  it('visar en kommande deadline', () => {
    expect(deadlineLine({dueAt: later, resubmitDueAt: [], gradeScale: 'PassFail'}, now))
      .toEqual({text: 'Deadline 10 nov', last: false})
  })

  it('visar passerad deadline och nästa komplettering', () => {
    expect(deadlineLine({dueAt: past, resubmitDueAt: [later], gradeScale: 'PassFail'}, now))
      .toEqual({text: 'Deadline 1 nov passerad · nästa komplettering 10 nov', last: false})
  })

  it('visar att sista deadline har passerat med betygen som skalan har', () => {
    expect(deadlineLine({dueAt: past, resubmitDueAt: [], gradeScale: 'PassFailDistinction'}, now))
      .toEqual({text: 'Sista deadline passerad 1 nov · bara G, VG eller IG', last: true})
    expect(deadlineLine({dueAt: past, resubmitDueAt: [], gradeScale: 'PassFail'}, now).text)
      .toContain('bara G eller IG')
  })

  it('säger det när uppgiften saknar deadline', () => {
    expect(deadlineLine({dueAt: null, resubmitDueAt: [], gradeScale: null}, now).text).toBe('Ingen deadline')
  })
})
