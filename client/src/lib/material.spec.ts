import {describe, expect, it} from 'vitest'

import {formatSize, isThisWeek, materialRows} from './material'

interface LinkExtra {
  title?: string
  authorName?: string
  courseId?: string | null
  groupId?: string | null
}

function link(linkId: string, contentId: string, publishAt: string, extra: LinkExtra = {}) {
  return {
    linkId,
    contentId,
    title: extra.title ?? `Del ${contentId}`,
    authorName: extra.authorName ?? 'Anna Lärare',
    courseId: extra.courseId ?? null,
    groupId: extra.groupId ?? null,
    publishAt,
  }
}

function attachment(id: string, contentId: string, fileName: string, sizeBytes = 1000) {
  return {id, contentId, fileName, url: `https://files.example/${id}`, sizeBytes}
}

describe('materialRows', () => {
  const names = new Map([['c1', 'DevOps'], ['c2', 'Databaser'], ['g1', 'Lag 1']])

  it('en rad per bilaga med del, mål och uppladdare', () => {
    const rows = materialRows([
      link('l1', 'x1', '2026-10-10T10:00:00Z', {title: 'Release-pipeline', courseId: 'c1'}),
      link('l2', 'x2', '2026-10-19T10:00:00Z', {title: 'Normalisering', courseId: 'c2'}),
    ], [
      attachment('a1', 'x1', 'rapport.pdf'),
      attachment('a2', 'x2', 'tabeller.xlsx'),
    ], names)

    expect(rows.map((row) => row.attachment.fileName)).toEqual(['tabeller.xlsx', 'rapport.pdf'])
    expect(rows[1].title).toBe('Release-pipeline')
    expect(rows[1].targets).toEqual(['DevOps'])
    expect(rows[1].authorName).toBe('Anna Lärare')
    expect(rows[1].publishAt).toBe('2026-10-10T10:00:00Z')
  })

  it('hoppar över en länk som kommer både från kursen och från gruppen', () => {
    const rows = materialRows([
      link('l1', 'x1', '2026-10-10T10:00:00Z', {courseId: 'c1'}),
      link('l1', 'x1', '2026-10-10T10:00:00Z', {courseId: 'c1'}),
      link('l2', 'x1', '2026-10-10T10:00:00Z', {groupId: 'g1'}),
    ], [attachment('a1', 'x1', 'rapport.pdf')], names)

    expect(rows).toHaveLength(1)
    expect(rows[0].targets).toEqual(['DevOps', 'Lag 1'])
  })

  it('sätter kursnamnen före gruppnamnen och använder den tidigaste publiceringen', () => {
    const rows = materialRows([
      link('l2', 'x1', '2026-10-01T10:00:00Z', {groupId: 'g1'}),
      link('l1', 'x1', '2026-10-10T10:00:00Z', {courseId: 'c1'}),
    ], [attachment('a1', 'x1', 'rapport.pdf')], names)

    expect(rows[0].targets).toEqual(['DevOps', 'Lag 1'])
    expect(rows[0].publishAt).toBe('2026-10-01T10:00:00Z')
  })

  it('visar inte bilagor till innehåll som saknas i flödena', () => {
    const rows = materialRows(
      [link('l1', 'x1', '2026-10-10T10:00:00Z', {courseId: 'c1'})],
      [attachment('a1', 'x1', 'rapport.pdf'), attachment('a2', 'x9', 'gammal.pdf')],
      names,
    )

    expect(rows.map((row) => row.attachment.id)).toEqual(['a1'])
  })
})

describe('isThisWeek', () => {
  const now = new Date('2026-10-20T12:00:00')

  it('räknar de senaste sju dygnen', () => {
    expect(isThisWeek('2026-10-19T10:00:00', now)).toBe(true)
    expect(isThisWeek('2026-10-14T12:00:00', now)).toBe(true)
    expect(isThisWeek('2026-10-01T10:00:00', now)).toBe(false)
  })

  it('räknar inte framtida filer som nya', () => {
    expect(isThisWeek('2026-10-21T10:00:00', now)).toBe(false)
  })
})

describe('formatSize', () => {
  it('byter enhet vid tusen och en miljon', () => {
    expect(formatSize(512)).toBe('512 B')
    expect(formatSize(2048)).toBe('2,0 kB')
    expect(formatSize(1_500_000)).toBe('1,5 MB')
  })

  it('använder svenskt komma med en decimal', () => {
    expect(formatSize(1250)).toBe('1,3 kB')
  })
})
