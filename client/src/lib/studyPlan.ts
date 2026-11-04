import type {Schema} from '@/lib/api'
import {autoFailLabel} from '@/lib/submissions'
import {gradeName} from '@/lib/submissions'

/** Svaret från GET /api/me/study-plan: poängsummor och kurserna eleven läser. */
export type StudyPlan = Schema<'StudyPlanDto'>
export type StudyPlanCourse = Schema<'StudyPlanCourseDto'>
export type StudyPlanAssignment = Schema<'StudyPlanAssignmentDto'>

const courseStatusLabels: Record<Schema<'StudyPlanCourseStatus'>, string> = {
  Completed: 'Klar',
  Ongoing: 'Pågår',
  Upcoming: 'Kommande',
}

const months = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec']

/** Statusen i kurslistan, t.ex. "Pågår". */
export function courseStatusLabel(status: Schema<'StudyPlanCourseStatus'>): string {
  return courseStatusLabels[status]
}

/** Betygets namn, t.ex. "Godkänd", eller "Ej satt" när kursdelen inte har något betyg än. */
export function gradeText(level: Schema<'GradeLevel'> | null): string {
  return level
    ? gradeName(level)
    : 'Ej satt'
}

/** Kursens period, t.ex. "okt – nov 2026" eller "nov 2025 – jan 2026". */
export function coursePeriod(course: {startsOn: string | null, endsOn: string | null}): string {
  if (course.startsOn && course.endsOn) {
    const start = monthAndYear(course.startsOn)
    const end = monthAndYear(course.endsOn)
    if (start.year === end.year && start.month === end.month) {
      return `${start.month} ${start.year}`
    }
    if (start.year === end.year) {
      return `${start.month} – ${end.month} ${end.year}`
    }
    return `${start.month} ${start.year} – ${end.month} ${end.year}`
  }
  if (course.startsOn) {
    const start = monthAndYear(course.startsOn)
    return `${start.month} ${start.year} –`
  }
  if (course.endsOn) {
    const end = monthAndYear(course.endsOn)
    return `– ${end.month} ${end.year}`
  }
  return '–'
}

/** Kort datum utan veckodag, t.ex. "23 okt". */
export function shortDate(value: string): string {
  if (/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const parts = value.split('-')
    return `${Number(parts[2])} ${months[Number(parts[1]) - 1]}`
  }
  return new Date(value)
    .toLocaleDateString('sv-SE', {day: 'numeric', month: 'short'})
    .replaceAll('.', '')
}

/** Inlämningens status i listan, t.ex. "Pågår · deadline 23 okt", "IG · Ingen inlämning" eller "Godkänd". */
export function assignmentStatus(assignment: StudyPlanAssignment, now = new Date()): string {
  if (assignment.deadline?.autoFail) {
    return autoFailLabel(assignment.status)
  }
  switch (assignment.status) {
    case 'Graded':
      return assignment.grade
        ? gradeName(assignment.grade)
        : 'Bedömd'
    case 'NeedsWork':
      return 'Komplettera'
    case 'Submitted':
      return 'Inlämnad'
    default:
      if (!assignment.dueAt) {
        return 'Inte inlämnad'
      }
      return deadlineText(assignment, now)
  }
}

// Backend räknar elevens nästa deadline, med kompletteringar och egen deadline. Passerad är den först när ingen återstår.
function deadlineText(assignment: StudyPlanAssignment, now: Date): string {
  const state = assignment.deadline
  if (!state) {
    return new Date(assignment.dueAt!) >= now
      ? `Pågår · deadline ${shortDate(assignment.dueAt!)}`
      : `Deadline passerade ${shortDate(assignment.dueAt!)}`
  }
  return state.nextDeadline
    ? `Pågår · deadline ${shortDate(state.nextDeadline)}`
    : `Deadline passerade ${shortDate(state.lastClassDeadline ?? assignment.dueAt!)}`
}

function monthAndYear(value: string): {month: string, year: string} {
  const parts = value.split('-')
  return {month: months[Number(parts[1]) - 1], year: parts[0]}
}
