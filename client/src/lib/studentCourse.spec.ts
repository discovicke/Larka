import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import {openableParts, sectionsFor} from '@/lib/studentCourse'

type Item = Schema<'StudentContentItem'>
type Module = Schema<'StudentModuleWithContentDto'>

function item(linkId: string, overrides: Partial<Item> = {}): Item {
  return {
    linkId,
    contentId: `c-${linkId}`,
    type: 'Theory',
    gradeScale: null,
    title: linkId,
    bodyMd: null,
    publishAt: '2026-01-01T08:00:00Z',
    dueAt: null,
    isPinned: false,
    isLocked: false,
    isCompleted: false,
    canMarkDone: true,
    blockedBy: [],
    submissionStatus: null,
    gradeLevel: null,
    ...overrides,
  } as Item
}

function week(content: Item[]): Module {
  return {id: 'm1', title: 'Vecka 1', isAvailable: true, content} as unknown as Module
}

const open = item('open')
const submission = item('sub', {type: 'Assignment', gradeScale: 'PassFail', canMarkDone: false})
const done = item('done', {isCompleted: true})
const news = item('news', {type: 'News'})
const unlocking = item('unlocking', {
  isLocked: true,
  blockedBy: [{linkId: 'open', title: 'open', isSatisfied: false}],
})
const later = item('later', {
  isLocked: true,
  blockedBy: [{linkId: 'unlocking', title: 'unlocking', isSatisfied: false}],
})
const everything = week([open, submission, done, news, unlocking, later])

const keys = (sections: ReturnType<typeof sectionsFor>) => sections.map((section) => section.key)
const linkIds = (sections: ReturnType<typeof sectionsFor>, key: string) =>
  sections.find((section) => section.key === key)?.items.map((candidate) => candidate.linkId)

describe('sectionsFor', () => {
  it('Allt delar upp veckan i nästa steg, låses upp härnäst, låst senare, nyheter och klart', () => {
    const sections = sectionsFor(everything, 'all')

    expect(keys(sections)).toEqual(['next', 'unlocking', 'later', 'news', 'completed'])
    expect(linkIds(sections, 'next')).toEqual(['open', 'sub'])
    expect(linkIds(sections, 'unlocking')).toEqual(['unlocking'])
    expect(linkIds(sections, 'later')).toEqual(['later'])
    expect(linkIds(sections, 'news')).toEqual(['news'])
    expect(linkIds(sections, 'completed')).toEqual(['done'])
  })

  it('En klar vecka visar bara Klart, utan tomma sektioner', () => {
    expect(keys(sectionsFor(week([done]), 'all'))).toEqual(['completed'])
  })

  it('tomt Nästa steg och Klart är hopfällda', () => {
    const emptyNext = sectionsFor(week([unlocking]), 'all')
    expect(emptyNext.find((section) => section.key === 'next')?.defaultOpen).toBe(false)

    const complete = sectionsFor(week([done]), 'all')
    expect(complete.find((section) => section.key === 'completed')?.defaultOpen).toBe(false)
  })

  it('Att göra nu visar bara nästa steg', () => {
    expect(keys(sectionsFor(everything, 'todo'))).toEqual(['next'])
  })

  it('Inlämningar behåller bara inlämningar och döljer tomma sektioner', () => {
    const sections = sectionsFor(everything, 'submissions')

    expect(keys(sections)).toEqual(['next'])
    expect(linkIds(sections, 'next')).toEqual(['sub'])
  })

  it.each([
    ['all', week([]), 'Inga publicerade delar i veckan än.'],
    ['all', week([unlocking]), 'Inga delar att göra just nu.'],
    ['submissions', week([open, done]), 'Inga inlämningar att göra just nu.'],
  ] as const)('tomtexten för %s', (filter, module, empty) => {
    expect(sectionsFor(module, filter)[0].empty).toBe(empty)
  })
})

describe('openableParts', () => {
  it('ger delarna i kursordning utan låsta delar och låsta veckor', () => {
    const modules = [
      {id: 'a', isAvailable: true, content: [item('x'), item('y', {isLocked: true})]},
      {id: 'b', isAvailable: false, content: [item('hidden')]},
      {id: 'c', isAvailable: true, content: [item('z')]},
    ] as unknown as Module[]
    expect(openableParts(modules).map((entry) => entry.part.linkId)).toEqual(['x', 'z'])
  })
})
