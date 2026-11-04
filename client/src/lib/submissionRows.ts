import {type Schema} from '@/lib/api'

type Submission = Schema<'SubmissionDto'>
type Group = Schema<'AssignmentGroupDto'>

export interface TrackingRow {
  key: string
  name: string
  group: Group | null
  submissions: Submission[]
  primary: Submission
  status: Schema<'SubmissionStatus'>
  /** Sista deadline har passerat utan inlämning eller komplettering: automatiskt IG. */
  autoFail: boolean
  /** Elevens egen extra deadline, för en elevrad. */
  personalDueAt: string | null
  repo: Submission['repo']
  submittedAt: string | null
  files: number
  grade: Submission['grade']
  gradedCount: number
  isMember: boolean
  supportReasons: string[]
}

export function aggregateStatus(submissions: Submission[]): Schema<'SubmissionStatus'> {
  if (submissions.length > 0 && submissions.every((submission) => submission.status === 'Graded')) {
    return 'Graded'
  }
  if (submissions.some((submission) => submission.status === 'NeedsWork')) {
    return 'NeedsWork'
  }
  if (submissions.some((submission) => submission.status === 'Submitted' || submission.status === 'Graded')) {
    return 'Submitted'
  }
  return 'NotStarted'
}

// Backend räknar automatiskt IG per elev. En grupp har det först när alla medlemmar har det.
export function groupAutoFail(submissions: Submission[]): boolean {
  return submissions.length > 0 && submissions.every((submission) => submission.deadline?.autoFail === true)
}

export function reasonsFor(submissions: Submission[]): string[] {
  const reasons = new Set<string>()
  for (const submission of submissions) {
    const support = submission.support
    if (!support) {
      continue
    }
    if (support.helpRequested) {
      reasons.add('Elevflagga')
    }
    if (support.notStarted) {
      reasons.add('Inte startat')
    }
    if (support.noRecentPush) {
      reasons.add('Ingen push')
    }
    if (support.redChecks) {
      reasons.add('Röda tester')
    }
  }
  return [...reasons]
}

export function preferredSubmission(submissions: Submission[]): Submission {
  return submissions.find((submission) => submission.status === 'Submitted')
    ?? submissions.find((submission) => submission.status === 'NeedsWork')
    ?? submissions[0]
}

export function submittedAtOf(submissions: Submission[]): string | null {
  return submissions
    .map((submission) => submission.submittedAt)
    .filter((value): value is string => Boolean(value))
    .sort()
    .at(-1) ?? null
}

export function sharedGrade(submissions: Submission[]): Submission['grade'] {
  if (submissions.length === 0 || submissions.some((submission) => submission.grade === null)) {
    return null
  }
  const level = submissions[0].grade?.level ?? null
  return submissions.every((submission) => (submission.grade?.level ?? null) === level)
    ? submissions[0].grade
    : null
}

export function toStudentRow(submission: Submission, isMember = false): TrackingRow {
  return {
    key: `student:${submission.id}`,
    name: submission.studentName,
    group: null,
    submissions: [submission],
    primary: submission,
    status: submission.status,
    autoFail: submission.deadline?.autoFail === true,
    personalDueAt: submission.personalDueAt ?? null,
    repo: submission.repo,
    submittedAt: submission.submittedAt,
    files: submission.files.length,
    grade: submission.grade,
    gradedCount: submission.grade
      ? 1
      : 0,
    isMember,
    supportReasons: reasonsFor([submission]),
  }
}

export function toGroupRow(group: Group, submissions: Submission[]): TrackingRow {
  return {
    key: `group:${group.id}`,
    name: group.name,
    group,
    submissions,
    primary: preferredSubmission(submissions),
    status: aggregateStatus(submissions),
    // Gruppens medlemmar kan ha olika personliga deadlines, så gruppen räknar med den senaste.
    autoFail: groupAutoFail(submissions),
    personalDueAt: null,
    repo: submissions.find((submission) => submission.repo)?.repo ?? null,
    submittedAt: submittedAtOf(submissions),
    files: submissions.reduce((sum, submission) => sum + submission.files.length, 0),
    grade: sharedGrade(submissions),
    gradedCount: submissions.filter((submission) => submission.grade).length,
    isMember: false,
    supportReasons: reasonsFor(submissions),
  }
}

// Radbyggandet delas av delens vy och inlämningsfliken, så att båda räknar en grupp som en rad.
export function buildRows(submissions: Submission[]): TrackingRow[] {
  const groups = new Map<string, {group: Group, submissions: Submission[]}>()
  const students: TrackingRow[] = []
  for (const submission of submissions) {
    if (submission.group) {
      const existing = groups.get(submission.group.id)
      if (existing) {
        existing.submissions.push(submission)
      } else {
        groups.set(submission.group.id, {group: submission.group, submissions: [submission]})
      }
    } else {
      students.push(toStudentRow(submission))
    }
  }
  return [
    ...students,
    ...[...groups.values()].map(({group, submissions: members}) => toGroupRow(group, members)),
  ]
}

// Att bedöma: rader med inlämning som väntar, där en grupp är en rad. Automatiskt IG väntar inte på någon bedömning.
export function countToGrade(rows: TrackingRow[]): number {
  return rows.filter((row) => row.status === 'Submitted' && !row.autoFail).length
}
