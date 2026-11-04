import type {Schema} from '@/lib/api'

export type GroupAnalytics = Schema<'GroupAnalyticsDto'>
export type AnalyticsCourse = Schema<'GroupAnalyticsCourseDto'>
export type AnalyticsStudent = Schema<'GroupAnalyticsStudentDto'>
export type AnalyticsStudentCourse = Schema<'GroupAnalyticsStudentCourseDto'>

export interface AnalyticsCounts {
  assignmentCount: number
  gradedCount: number
  failCount: number
  passCount: number
  distinctionCount: number
  needsWorkCount: number
  submittedCount: number
  overdueCount: number
  autoFailCount: number
}

export interface AnalyticsSummary extends AnalyticsCounts {
  studentCount: number
  atRiskStudentCount: number
  notStartedCount: number
  passRate: number
}

export interface StudentAnalyticsRow extends AnalyticsCounts {
  studentId: string
  name: string
  needsSupport: boolean
}

export type StudentProgressStatus = 'needsSupport' | 'complete' | 'onTrack'

/** En elev är klar först när hela det valda kursomfånget är avslutat och allt är bedömt utan IG. */
export function studentProgressStatus(row: StudentAnalyticsRow, scopeFinished: boolean): StudentProgressStatus {
  if (row.needsSupport) {
    return 'needsSupport'
  }
  return scopeFinished && row.assignmentCount > 0 && row.gradedCount === row.assignmentCount
    ? 'complete'
    : 'onTrack'
}

export interface ChartDatum {
  key: string
  label: string
  count: number
  color: string
}

export const chartColors = {
  fail: 'var(--destructive)',
  pass: 'var(--success)',
  distinction: 'var(--info)',
  ungraded: 'var(--chart-5)',
  submitted: 'var(--info)',
  needsWork: 'var(--warning)',
  missing: 'var(--destructive)',
  overdue: 'var(--chart-5)',
  notStarted: 'var(--chart-5)',
}

const emptyCounts = (): AnalyticsCounts => ({
  assignmentCount: 0,
  gradedCount: 0,
  failCount: 0,
  passCount: 0,
  distinctionCount: 0,
  needsWorkCount: 0,
  submittedCount: 0,
  overdueCount: 0,
  autoFailCount: 0,
})

function addCounts(target: AnalyticsCounts, source: AnalyticsCounts) {
  target.assignmentCount += source.assignmentCount
  target.gradedCount += source.gradedCount
  target.failCount += source.failCount
  target.passCount += source.passCount
  target.distinctionCount += source.distinctionCount
  target.needsWorkCount += source.needsWorkCount
  target.submittedCount += source.submittedCount
  target.overdueCount += source.overdueCount
  target.autoFailCount += source.autoFailCount
}

function rowsForStudent(student: AnalyticsStudent, courseId: string): AnalyticsStudentCourse[] {
  return courseId
    ? student.courses.filter((course) => course.courseId === courseId)
    : student.courses
}

export function studentRows(analytics: GroupAnalytics, courseId: string): StudentAnalyticsRow[] {
  return analytics.students.map((student) => {
    const counts = emptyCounts()
    const rows = rowsForStudent(student, courseId)
    for (const row of rows) {
      addCounts(counts, row)
    }
    return {
      studentId: student.studentId,
      name: student.name,
      needsSupport: courseId
        ? rows.some((row) => row.needsSupport)
        : student.needsSupport,
      ...counts,
    }
  })
}

export function summarize(analytics: GroupAnalytics, courseId: string): AnalyticsSummary {
  const rows = studentRows(analytics, courseId)
  const counts = emptyCounts()
  for (const row of rows) {
    addCounts(counts, row)
  }
  const accountedFor = counts.gradedCount
    + counts.needsWorkCount
    + counts.submittedCount
    + counts.overdueCount
  return {
    ...counts,
    studentCount: rows.length,
    atRiskStudentCount: rows.filter((row) => row.needsSupport).length,
    notStartedCount: Math.max(0, counts.assignmentCount - accountedFor),
    // Ett automatiskt IG är redan bedömt och räknat som IG, så det drar ner andelen godkända.
    passRate: counts.gradedCount === 0
      ? 0
      : (counts.passCount + counts.distinctionCount) / counts.gradedCount,
  }
}

export function gradeDistribution(summary: AnalyticsSummary): ChartDatum[] {
  return [
    {key: 'fail', label: 'IG', count: summary.failCount, color: chartColors.fail},
    {key: 'pass', label: 'G', count: summary.passCount, color: chartColors.pass},
    {key: 'distinction', label: 'VG', count: summary.distinctionCount, color: chartColors.distinction},
    {
      key: 'ungraded',
      label: 'Ej bedömt',
      count: Math.max(0, summary.assignmentCount - summary.gradedCount),
      color: chartColors.ungraded,
    },
  ]
}

export function statusDistribution(summary: AnalyticsSummary): ChartDatum[] {
  return [
    // Automatiskt IG ingår i gradedCount men har en egen del, så att uppgiften inte syns två gånger.
    {key: 'graded', label: 'Bedömt', count: summary.gradedCount - summary.autoFailCount, color: chartColors.pass},
    {key: 'submitted', label: 'Att bedöma', count: summary.submittedCount, color: chartColors.submitted},
    {key: 'needsWork', label: 'Komplettering', count: summary.needsWorkCount, color: chartColors.needsWork},
    {key: 'autoFail', label: 'IG (ingen inlämning)', count: summary.autoFailCount, color: chartColors.missing},
    {key: 'overdue', label: 'Försenat', count: summary.overdueCount, color: chartColors.overdue},
    {key: 'notStarted', label: 'Ej påbörjat', count: summary.notStartedCount, color: chartColors.notStarted},
  ]
}

export function courseGradeData(courses: AnalyticsCourse[]) {
  return courses.map((course) => ({
    code: course.code,
    title: course.title,
    fail: course.failCount,
    pass: course.passCount,
    distinction: course.distinctionCount,
    atRisk: course.atRiskStudentCount,
  }))
}

export function percent(value: number): string {
  return `${Math.round(value * 100)} %`
}
