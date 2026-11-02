import {describe, expect, it} from 'vitest'

import {type GradingRow, nextGradingRow} from '@/lib/gradingQueue'

function row(linkId: string, name: string): GradingRow {
  return {
    submission: {id: `${linkId}-${name}`} as GradingRow['submission'],
    courseId: 'c1',
    courseTitle: 'DevOps',
    linkId,
    title: linkId,
    name,
  }
}

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
