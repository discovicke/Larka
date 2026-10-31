import {describe, expect, it} from 'vitest'

import {dayGroup, mergeNews, sortNews} from './news'

function post(linkId: string, publishAt: string, isPinned = false) {
  return {linkId, publishAt, isPinned}
}

describe('sortNews', () => {
  it('lägger fästa först och sedan nyast först', () => {
    const sorted = sortNews([
      post('äldst', '2026-10-01T10:00:00Z'),
      post('fäst', '2026-10-19T10:00:00Z', true),
      post('nyast', '2026-10-10T10:00:00Z'),
    ])

    expect(sorted.map((item) => item.linkId)).toEqual(['fäst', 'nyast', 'äldst'])
  })

  it('ändrar inte listan den får', () => {
    const posts = [post('a', '2026-10-01T10:00:00Z'), post('b', '2026-10-02T10:00:00Z')]

    const sorted = sortNews(posts)

    expect(sorted).not.toBe(posts)
    expect(posts.map((item) => item.linkId)).toEqual(['a', 'b'])
  })
})

describe('mergeNews', () => {
  function item(
    linkId: string,
    contentId: string,
    publishAt: string,
    extra: {courseId?: string | null, groupId?: string | null, isPinned?: boolean} = {},
  ) {
    return {
      linkId,
      contentId,
      courseId: extra.courseId ?? null,
      groupId: extra.groupId ?? null,
      publishAt,
      isPinned: extra.isPinned ?? false,
    }
  }

  it('slår ihop länkar till samma innehåll och behåller alla mål', () => {
    const names = new Map([['c1', 'DevOps'], ['g1', 'Lag 1']])

    const merged = mergeNews([
      item('l1', 'x1', '2026-10-01T10:00:00Z', {courseId: 'c1'}),
      item('l2', 'x1', '2026-10-01T10:00:00Z', {groupId: 'g1'}),
    ], names)

    expect(merged).toHaveLength(1)
    expect(merged[0].links.map((link) => link.linkId)).toEqual(['l1', 'l2'])
    expect(merged[0].post.linkId).toBe('l1')
    expect(merged[0].targets).toEqual(['DevOps', 'Lag 1'])
  })

  it('tar bara med en länk en gång när den finns i både kursens och gruppens flöde', () => {
    const names = new Map([['c1', 'DevOps']])

    const merged = mergeNews([
      item('l1', 'x1', '2026-10-01T10:00:00Z', {courseId: 'c1'}),
      item('l1', 'x1', '2026-10-01T10:00:00Z', {courseId: 'c1'}),
    ], names)

    expect(merged[0].links.map((link) => link.linkId)).toEqual(['l1'])
    expect(merged[0].targets).toEqual(['DevOps'])
  })

  it('räknar posten som fäst om någon länk är fäst och sorterar fästa först', () => {
    const merged = mergeNews([
      item('a', 'x2', '2026-10-10T10:00:00Z'),
      item('b', 'x1', '2026-10-01T10:00:00Z', {isPinned: true}),
    ], new Map())

    expect(merged.map((entry) => entry.post.contentId)).toEqual(['x1', 'x2'])
    expect(merged[0].post.isPinned).toBe(true)
    expect(merged[1].post.isPinned).toBe(false)
  })

  it('sorterar nyast först och hoppar över mål som saknar namn', () => {
    const merged = mergeNews([
      item('a', 'x1', '2026-10-01T10:00:00Z', {courseId: 'okänd'}),
      item('b', 'x2', '2026-10-10T10:00:00Z', {courseId: 'c1'}),
    ], new Map([['c1', 'DevOps']]))

    expect(merged.map((entry) => entry.post.contentId)).toEqual(['x2', 'x1'])
    expect(merged[0].targets).toEqual(['DevOps'])
    expect(merged[1].targets).toEqual([])
  })
})

describe('dayGroup', () => {
  const now = new Date('2026-10-20T12:00:00')

  it('räknar lokala kalenderdagar', () => {
    expect(dayGroup('2026-10-20T00:05:00', now)).toBe('Idag')
    expect(dayGroup('2026-10-19T23:55:00', now)).toBe('Igår')
    expect(dayGroup('2026-10-18T23:55:00', now)).toBe('Tidigare')
  })

  it('klarar dygnen kring sommartid', () => {
    const afterDst = new Date('2026-10-26T12:00:00')

    expect(dayGroup('2026-10-25T12:00:00', afterDst)).toBe('Igår')
    expect(dayGroup('2026-10-24T12:00:00', afterDst)).toBe('Tidigare')
  })
})
