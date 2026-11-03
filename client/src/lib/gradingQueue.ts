import {type Schema} from '@/lib/api'
import {gradeLabel, levelsFor} from '@/lib/submissions'
import {fetchCourseModules} from '@/lib/modulesApi'
import {fetchLinkSubmissions} from '@/lib/submissionsApi'

export interface GradingRow {
  submission: Schema<'SubmissionDto'>
  courseId: string
  courseTitle: string
  linkId: string
  title: string
  name: string
  /** Uppgiftens deadlines. Utan dueAt gäller ingen deadlinelogik. */
  dueAt: string | null
  resubmitDueAt: string[]
  gradeScale: Schema<'GradeScale'> | null
  /** Sant när en tidigare bedömning finns: läraren har begärt komplettering förut. */
  previousGrade: boolean
}

export type GradingTab = 'submitted' | 'waiting'

export interface GradingGroup {
  linkId: string
  courseId: string
  title: string
  courseTitle: string
  dueAt: string | null
  resubmitDueAt: string[]
  gradeScale: Schema<'GradeScale'> | null
  rows: GradingRow[]
}

/** Alla inlämnade inlämningar i lärarens kurser, i kursernas och uppgifternas ordning. Används av kön i bedömningen. */
export async function fetchGradingRows(courses: Schema<'CourseDto'>[]): Promise<GradingRow[]> {
  return (await fetchAssignmentRows(courses)).filter((row) => row.submission.status === 'Submitted')
}

/** Elevernas inlämningar i alla lärarens uppgifter, oavsett läge. Fliken i Att bedöma sorterar dem sedan. */
export async function fetchAssignmentRows(courses: Schema<'CourseDto'>[]): Promise<GradingRow[]> {
  const moduleResults = await Promise.all(courses.map((course) => fetchCourseModules(course.id)))

  const assignments = moduleResults.flatMap((weeks, index) => {
    const course = courses[index]
    return weeks.flatMap((week) => week.content
      .filter((item) => item.type === 'Assignment')
      .map((item) => ({course, item})))
  })

  const submissionResults = await Promise.all(assignments.map(({item}) => fetchLinkSubmissions(item.linkId)))

  return assignments.flatMap(({course, item}, index) => submissionResults[index]
    .filter((submission) => submission.status !== 'Graded')
    .map((submission) => ({
      submission,
      courseId: course.id,
      courseTitle: course.title,
      linkId: item.linkId,
      title: item.title,
      name: submission.studentName,
      dueAt: item.dueAt ?? null,
      resubmitDueAt: item.resubmitDueAt ?? [],
      gradeScale: item.gradeScale ?? null,
      previousGrade: submission.grade != null,
    })))
}

// Klassens nästa deadline (huvuddeadline eller komplettering) för rubriken. Elevens läge kommer från backend.
function nextClassDeadline(group: Pick<GradingGroup, 'dueAt' | 'resubmitDueAt'>, now: Date): string | null {
  return [group.dueAt, ...group.resubmitDueAt]
    .filter((value): value is string => Boolean(value) && new Date(value as string) > now)
    .sort((first, second) => new Date(first).getTime() - new Date(second).getTime())[0] ?? null
}

/** Vilken flik en rad hör till, eller null när läraren inget har att göra eller se. */
export function gradingTabOf(row: GradingRow, now = new Date()): GradingTab | null {
  const status = row.submission.status
  if (status === 'Submitted') {
    return 'submitted'
  }
  // Automatiskt IG kräver ingen åtgärd av läraren och visas inte här. Det syns i uppgiftens inlämningslista.
  if (row.submission.deadline?.autoFail) {
    return null
  }
  if (status === 'NeedsWork') {
    return 'waiting'
  }
  return status === 'NotStarted' && row.dueAt && new Date(row.dueAt) <= now
    ? 'waiting'
    : null
}

/** Rader per flik. Flikarnas antal är längden på listorna. */
export function groupRowsByTab(rows: GradingRow[], now = new Date()): Record<GradingTab, GradingRow[]> {
  const tabs: Record<GradingTab, GradingRow[]> = {submitted: [], waiting: []}
  for (const row of rows) {
    const tab = gradingTabOf(row, now)
    if (tab) {
      tabs[tab].push(row)
    }
  }
  return tabs
}

/** Raderna per uppgift i uppgifternas ordning, och inom uppgiften på namn, så som kön i bedömningen går. */
export function groupByAssignment(rows: GradingRow[]): GradingGroup[] {
  const groups = new Map<string, GradingGroup>()
  for (const row of rows) {
    const group = groups.get(row.linkId) ?? {
      linkId: row.linkId,
      courseId: row.courseId,
      title: row.title,
      courseTitle: row.courseTitle,
      dueAt: row.dueAt,
      resubmitDueAt: row.resubmitDueAt,
      gradeScale: row.gradeScale,
      rows: [],
    }
    group.rows.push(row)
    groups.set(row.linkId, group)
  }
  return [...groups.values()].map((group) => ({
    ...group,
    rows: [...group.rows].sort((left, right) => left.name.localeCompare(right.name, 'sv')),
  }))
}

function shortDate(iso: string): string {
  return new Date(iso)
    .toLocaleDateString('sv-SE', {day: 'numeric', month: 'short'})
    .replaceAll('.', '')
}

export interface DeadlineLine {
  text: string
  /** Sista deadline har passerat: markeras i rött. */
  last: boolean
}

/** Raden under uppgiftens rubrik: var uppgiften står mot deadlinerna. */
export function deadlineLine(
  group: Pick<GradingGroup, 'dueAt' | 'resubmitDueAt' | 'gradeScale'>,
  now = new Date(),
): DeadlineLine {
  if (!group.dueAt) {
    return {text: 'Ingen deadline', last: false}
  }
  const upcoming = nextClassDeadline(group, now)
  if (new Date(group.dueAt) > now) {
    return {text: `Deadline ${shortDate(group.dueAt)}`, last: false}
  }
  if (upcoming) {
    return {
      text: `Deadline ${shortDate(group.dueAt)} passerad · nästa komplettering ${shortDate(upcoming)}`,
      last: false,
    }
  }
  const lastDeadline = [group.dueAt, ...group.resubmitDueAt]
    .reduce((latest, value) => new Date(value) > new Date(latest)
      ? value
      : latest)
  const passing = levelsFor(group.gradeScale)
    .filter((level) => level !== 'Fail')
    .map((level) => gradeLabel(level))
  const allowed = passing.length > 0
    ? `${passing.join(', ')} eller IG`
    : 'G, VG eller IG'
  return {text: `Sista deadline passerad ${shortDate(lastDeadline)} · bara ${allowed}`, last: true}
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
