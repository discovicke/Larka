import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import {buildRows, countToGrade} from '@/lib/submissionRows'
import {deadlineState} from '@/test/deadline'

const group = {id: 'g1', name: 'Grupp 1', members: []} as unknown as Schema<'AssignmentGroupDto'>

function submission(
  id: string,
  status: Schema<'SubmissionStatus'>,
  extra: Partial<Schema<'SubmissionDto'>> = {},
) {
  return {
    id,
    studentId: id,
    studentName: id,
    status,
    files: [],
    repo: null,
    grade: null,
    submittedAt: null,
    deadline: deadlineState(),
    ...extra,
  } as unknown as Schema<'SubmissionDto'>
}

describe('countToGrade', () => {
  it('räknar en grupp som en rad även när flera medlemmar har lämnat in', () => {
    const rows = buildRows([
      submission('a', 'Submitted', {group}),
      submission('b', 'Submitted', {group}),
      submission('c', 'Submitted'),
      submission('d', 'NotStarted'),
    ])

    expect(rows).toHaveLength(3)
    expect(countToGrade(rows)).toBe(2)
  })

  it('räknar inte en grupp där en medlem är komplettering eller alla är bedömda', () => {
    expect(countToGrade(buildRows([
      submission('a', 'Submitted', {group}),
      submission('b', 'NeedsWork', {group}),
    ]))).toBe(0)
    expect(countToGrade(buildRows([
      submission('a', 'Graded', {group}),
      submission('b', 'Graded', {group}),
    ]))).toBe(0)
  })

  it('räknar inte automatiskt IG', () => {
    expect(countToGrade(buildRows([
      submission('a', 'Submitted', {deadline: deadlineState({autoFail: true})}),
    ]))).toBe(0)
  })
})
