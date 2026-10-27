import {describe, expect, it} from 'vitest'

import {initials, paragraphs, sortNews} from './news'

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

describe('initials', () => {
  it('tar första bokstaven i för- och efternamn', () => {
    expect(initials('Anna Lärare')).toBe('AL')
  })

  it('klarar ett namn och extra mellanslag', () => {
    expect(initials('Anna')).toBe('A')
    expect(initials(' Anna  Lärare ')).toBe('AL')
  })

  it('visar frågetecken när namnet saknas', () => {
    expect(initials('')).toBe('?')
  })
})

describe('paragraphs', () => {
  it('delar på tomma rader och trimmar', () => {
    expect(paragraphs('Första stycket.\n\n  Andra stycket.  ')).toEqual(['Första stycket.', 'Andra stycket.'])
  })

  it('hoppar över tomma stycken', () => {
    expect(paragraphs('Bara ett stycke.')).toEqual(['Bara ett stycke.'])
    expect(paragraphs('\n\n  \n\n')).toEqual([])
  })
})
