import {describe, expect, it} from 'vitest'

import {weekPublishState} from '@/lib/content'

const now = new Date('2026-10-27T12:00:00Z')

function part(status: 'Draft' | 'Published', publishAt: string) {
  return {status, publishAt}
}

describe('weekPublishState', () => {
  it('visar utkast när alla delar är utkast, också med ett framtida datum', () => {
    expect(weekPublishState({
      publishAt: '2026-11-03T08:00:00Z',
      content: [part('Draft', '2026-11-03T08:00:00Z')],
    }, now)).toEqual({status: 'publishing.draft'})
  })

  it('visar veckans datum före delarnas status', () => {
    const state = weekPublishState({
      publishAt: '2026-11-03T08:00:00Z',
      content: [part('Published', '2026-10-01T08:00:00Z')],
    }, now)

    expect(state).toMatchObject({status: 'publishing.scheduled'})
    expect(state?.label).toContain('Schemalagd')
  })

  it('visar publicerad när alla delar är live', () => {
    expect(weekPublishState({
      publishAt: null,
      content: [
        part('Published', '2026-10-01T08:00:00Z'),
        part('Published', '2026-10-02T08:00:00Z'),
      ],
    }, now)).toMatchObject({status: 'publishing.published'})
  })

  it('visar delvis publicerad när bara några delar är live', () => {
    expect(weekPublishState({
      publishAt: null,
      content: [
        part('Published', '2026-10-01T08:00:00Z'),
        part('Draft', '2026-10-02T08:00:00Z'),
      ],
    }, now)).toMatchObject({status: 'publishing.partial'})
  })

  it('visar nästa publiceringsdatum när ingen del är live', () => {
    const state = weekPublishState({
      publishAt: null,
      content: [
        part('Published', '2026-11-03T08:00:00Z'),
        part('Published', '2026-11-05T08:00:00Z'),
      ],
    }, now)

    expect(state).toMatchObject({status: 'publishing.scheduled'})
    expect(state?.label).toContain('Schemalagd')
  })

  it('visar utkast när alla delar är utkast', () => {
    expect(weekPublishState({
      publishAt: null,
      content: [part('Draft', '2026-10-01T08:00:00Z')],
    }, now)).toMatchObject({status: 'publishing.draft'})
  })

  it('visar utkast för en ny, tom vecka', () => {
    expect(weekPublishState({publishAt: null, content: []}, now)).toEqual({status: 'publishing.draft'})
  })
})
