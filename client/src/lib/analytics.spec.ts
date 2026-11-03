import {describe, expect, it} from 'vitest'

import type {GroupAnalytics} from '@/lib/analytics'
import {gradeDistribution, statusDistribution, studentProgressStatus, studentRows, summarize} from '@/lib/analytics'

const analytics: GroupAnalytics = {
  groupId: 'g1',
  groupName: 'TE22A',
  courses: [
    {
      courseId: 'c1',
      title: 'DevOps',
      code: 'DEVOPS',
      startsOn: '2026-10-01',
      endsOn: '2026-11-01',
      studentCount: 2,
      assignmentCount: 3,
      gradedCount: 2,
      failCount: 1,
      passCount: 1,
      distinctionCount: 0,
      needsWorkCount: 1,
      submittedCount: 1,
      overdueCount: 1,
      notSubmittedCount: 0,
      atRiskStudentCount: 2,
    },
    {
      courseId: 'c2',
      title: 'Databaser',
      code: 'DB',
      startsOn: '2026-11-01',
      endsOn: '2026-12-01',
      studentCount: 2,
      assignmentCount: 1,
      gradedCount: 1,
      failCount: 0,
      passCount: 1,
      distinctionCount: 0,
      needsWorkCount: 0,
      submittedCount: 0,
      overdueCount: 0,
      notSubmittedCount: 0,
      atRiskStudentCount: 0,
    },
  ],
  students: [
    {
      studentId: 's1',
      name: 'Anna Elev',
      needsSupport: true,
      courses: [
        {
          courseId: 'c1',
          assignmentCount: 3,
          gradedCount: 1,
          failCount: 1,
          passCount: 0,
          distinctionCount: 0,
          needsWorkCount: 1,
          submittedCount: 1,
          overdueCount: 1,
          notSubmittedCount: 0,
          needsSupport: true,
        },
        {
          courseId: 'c2',
          assignmentCount: 1,
          gradedCount: 1,
          failCount: 0,
          passCount: 1,
          distinctionCount: 0,
          needsWorkCount: 0,
          submittedCount: 0,
          overdueCount: 0,
          notSubmittedCount: 0,
          needsSupport: false,
        },
      ],
    },
    {
      studentId: 's2',
      name: 'Bo Elev',
      needsSupport: false,
      courses: [
        {
          courseId: 'c1',
          assignmentCount: 3,
          gradedCount: 1,
          failCount: 0,
          passCount: 1,
          distinctionCount: 0,
          needsWorkCount: 0,
          submittedCount: 0,
          overdueCount: 0,
          notSubmittedCount: 0,
          needsSupport: false,
        },
        {
          courseId: 'c2',
          assignmentCount: 1,
          gradedCount: 0,
          failCount: 0,
          passCount: 0,
          distinctionCount: 0,
          needsWorkCount: 0,
          submittedCount: 0,
          overdueCount: 0,
          notSubmittedCount: 0,
          needsSupport: false,
        },
      ],
    },
  ],
}

describe('analytics', () => {
  it('summerar en vald kurs och räknar elever i riskzonen', () => {
    const summary = summarize(analytics, 'c1')

    expect(summary.studentCount).toBe(2)
    expect(summary.assignmentCount).toBe(6)
    expect(summary.gradedCount).toBe(2)
    expect(summary.failCount).toBe(1)
    expect(summary.passCount).toBe(1)
    expect(summary.needsWorkCount).toBe(1)
    expect(summary.atRiskStudentCount).toBe(1)
    expect(summary.passRate).toBe(0.5)
  })

  it('summerar alla kurser när inget kursfilter är valt', () => {
    const summary = summarize(analytics, '')

    expect(summary.assignmentCount).toBe(8)
    expect(summary.gradedCount).toBe(3)
    expect(summary.atRiskStudentCount).toBe(1)
    expect(summary.notStartedCount).toBe(2)
  })

  it('filtrerar elevrader på vald kurs', () => {
    const rows = studentRows(analytics, 'c2')

    expect(rows.map((row) => [row.name, row.gradedCount, row.needsSupport])).toEqual([
      ['Anna Elev', 1, false],
      ['Bo Elev', 0, false],
    ])
  })

  it('markerar en elev som klar först när avslutad kurs är helt bedömd', () => {
    const complete = {
      studentId: 's1',
      name: 'Anna Elev',
      needsSupport: false,
      assignmentCount: 4,
      gradedCount: 4,
      failCount: 0,
      passCount: 3,
      distinctionCount: 1,
      needsWorkCount: 0,
      submittedCount: 0,
      overdueCount: 0,
      notSubmittedCount: 0,
    }

    expect(studentProgressStatus(complete, true)).toBe('complete')
    expect(studentProgressStatus(complete, false)).toBe('onTrack')
    expect(studentProgressStatus({...complete, needsSupport: true, failCount: 1}, true))
      .toBe('needsSupport')
  })

  it('bygger betygs- och statusdata för charts', () => {
    const summary = summarize(analytics, 'c1')

    expect(gradeDistribution(summary).map((item) => [item.label, item.count])).toEqual([
      ['IG', 1],
      ['G', 1],
      ['VG', 0],
      ['Ej bedömt', 4],
    ])
    expect(statusDistribution(summary).map((item) => [item.label, item.count])).toEqual([
      ['Bedömt', 2],
      ['Att bedöma', 1],
      ['Komplettering', 1],
      ['Ej inlämnat', 0],
      ['Försenat', 1],
      ['Ej påbörjat', 1],
    ])
  })
})
