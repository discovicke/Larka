import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import {deadlineLine, type GradingRow, groupByAssignment, gradingTabOf, groupRowsByTab, nextGradingRow} from '@/lib/gradingQueue'
import {deadlineState} from '@/test/deadline'

function row(
  linkId: string,
  name: string,
  extra: Partial<GradingRow> = {},
  status = 'Submitted',
  deadline: Partial<Schema<'DeadlineStateDto'>> = {},
): GradingRow {
  return {
    submission: {id: `${linkId}-${name}`, status, deadline: deadlineState(deadline)} as GradingRow['submission'],
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

  it('lägger inte påbörjad i Väntar på eleven först när huvuddeadlinen har passerat och en chans återstår', () => {
    const open = {dueAt: past, resubmitDueAt: [later]}
    expect(gradingTabOf(row('a', 'Ada', open, 'NotStarted'), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', {dueAt: later}, 'NotStarted'), now)).toBeNull()
    expect(gradingTabOf(row('a', 'Ada', {}, 'NotStarted'), now)).toBeNull()
  })

  it('räknar elevens egen deadline: eleven väntar i stället för att få automatiskt IG', () => {
    const personal = {nextDeadline: later}
    expect(gradingTabOf(row('a', 'Ada', {dueAt: past}, 'NotStarted', personal), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', {dueAt: past}, 'NeedsWork', personal), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', {dueAt: past}, 'NotStarted', {autoFail: true}), now)).toBeNull()
  })

  it('lägger uteblivet arbete efter sista deadline i ingen flik', () => {
    expect(gradingTabOf(row('a', 'Ada', {dueAt: past}, 'NotStarted', {autoFail: true}), now)).toBeNull()
  })

  it('skiljer automatiskt IG (ingen flik) från Väntar på eleven på om en deadline återstår', () => {
    const group = {dueAt: past, resubmitDueAt: [later]}
    const open = {nextDeadline: later}
    const closed = {autoFail: true}
    expect(gradingTabOf(row('a', 'Ada', group, 'NeedsWork', open), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', group, 'NotStarted', open), now)).toBe('waiting')
    expect(gradingTabOf(row('a', 'Ada', group, 'NeedsWork', closed), now)).toBeNull()
    expect(gradingTabOf(row('a', 'Ada', group, 'NotStarted', closed), now)).toBeNull()
  })

  it('räknar rader per flik', () => {
    const tabs = groupRowsByTab([
      row('a', 'Ada'),
      row('a', 'Bo', {dueAt: past}, 'NotStarted', {autoFail: true}),
      row('a', 'Cia', {dueAt: past}, 'NeedsWork', {autoFail: true}),
    ], now)
    expect(tabs.submitted).toHaveLength(1)
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
    expect(deadlineLine({dueAt: later, resubmitDueAt: []}, now))
      .toEqual({text: 'Deadline 10 nov', last: false})
  })

  it('visar passerad deadline och nästa komplettering', () => {
    expect(deadlineLine({dueAt: past, resubmitDueAt: [later]}, now))
      .toEqual({text: 'Deadline 1 nov passerad · nästa komplettering 10 nov', last: false})
  })

  it('visar att sista deadline har passerat', () => {
    expect(deadlineLine({dueAt: past, resubmitDueAt: []}, now))
      .toEqual({text: 'Sista deadline 1 nov har passerat', last: true})
  })

  it('säger det när uppgiften saknar deadline', () => {
    expect(deadlineLine({dueAt: null, resubmitDueAt: []}, now).text).toBe('Ingen deadline')
  })
})
