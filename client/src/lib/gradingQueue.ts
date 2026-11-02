import {type Schema} from '@/lib/api'
import {fetchCourseModules} from '@/lib/modulesApi'
import {fetchLinkSubmissions} from '@/lib/submissionsApi'

export interface GradingRow {
  submission: Schema<'SubmissionDto'>
  courseId: string
  courseTitle: string
  linkId: string
  title: string
  name: string
}

/** Alla inlämnade inlämningar i lärarens kurser, i kursernas och uppgifternas ordning. Samma data som Att bedöma. */
export async function fetchGradingRows(courses: Schema<'CourseDto'>[]): Promise<GradingRow[]> {
  const moduleResults = await Promise.all(courses.map((course) => fetchCourseModules(course.id)))

  const assignments = moduleResults.flatMap((weeks, index) => {
    const course = courses[index]
    return weeks.flatMap((week) => week.content
      .filter((item) => item.type === 'Assignment')
      .map((item) => ({course, item})))
  })

  const submissionResults = await Promise.all(assignments.map(({item}) => fetchLinkSubmissions(item.linkId)))

  return assignments.flatMap(({course, item}, index) => submissionResults[index]
    .filter((submission) => submission.status === 'Submitted')
    .map((submission) => ({
      submission,
      courseId: course.id,
      courseTitle: course.title,
      linkId: item.linkId,
      title: item.title,
      name: submission.studentName,
    })))
}

/**
 * Första inlämnade inlämningen i nästa uppgift som har obedömt arbete. Letar först efter den uppgift
 * som ligger efter den aktuella och börjar annars om från början. Null när inget annat väntar.
 */
export function nextGradingRow(rows: GradingRow[], currentLinkId: string): GradingRow | null {
  const others = rows.filter((row) => row.linkId !== currentLinkId)
  const order = [...new Set(rows.map((row) => row.linkId))]
  const position = order.indexOf(currentLinkId)
  const after = others.filter((row) => order.indexOf(row.linkId) > position)
  const pool = position >= 0 && after.length > 0
    ? after
    : others
  const first = pool.at(0)?.linkId
  return pool
    .filter((row) => row.linkId === first)
    .sort((left, right) => left.name.localeCompare(right.name, 'sv'))
    .at(0) ?? null
}
