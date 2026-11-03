import {
  PhCalendarBlank,
  PhCheckCircle,
  PhClock,
  PhLock,
  PhPaperPlaneTilt,
  PhWarning,
  PhWarningCircle,
  PhXCircle,
} from '@phosphor-icons/vue'
import type {Component} from 'vue'

import type {BadgeVariants} from '@/components/ui/badge'
import type {Schema} from '@/lib/api'
import {courseStatusLabels} from '@/lib/courseStatus'
import {isDueSoon, isOverdue} from '@/lib/today'

/**
 * Appens alla lägen på ett ställe: svensk text, ton och eventuell ikon.
 * Tonen är det enda sättet att välja färg, så samma betydelse ger alltid samma färg.
 * Reglerna står i docs/design.md under "Status".
 */
export type StatusTone = 'neutral' | 'info' | 'warning' | 'destructive' | 'success'

export type StatusFamily = 'course' | 'publishing' | 'progress' | 'submission' | 'deadline'

export interface StatusDefinition {
  family: StatusFamily
  label: string
  tone: StatusTone
  icon?: Component
}

/** Toner och deras enda betydelse. Betygsstämplarna IG, G och VG hör inte hit. */
export const toneMeanings: Record<StatusTone, string> = {
  neutral: 'Information, inget att göra',
  info: 'Väntar på någon annan',
  warning: 'Behöver uppmärksamhet snart',
  destructive: 'Behöver åtgärd nu',
  success: 'Eleven är klar',
}

type BadgeVariant = NonNullable<BadgeVariants['variant']>

/** Tonen som Badge-variant. Neutralt är en kontur, så att normala lägen är tysta. */
export const toneVariants: Record<StatusTone, BadgeVariant> = {
  neutral: 'outline',
  info: 'info',
  warning: 'warning',
  destructive: 'destructive',
  success: 'success',
}

const definitions = {
  'course.upcoming': {family: 'course', label: courseStatusLabels.upcoming, tone: 'neutral'},
  'course.running': {family: 'course', label: courseStatusLabels.running, tone: 'neutral'},
  'course.finished': {family: 'course', label: courseStatusLabels.finished, tone: 'neutral'},
  // Saknad inställning: admin ska utse en lärare, och ingen annan kan göra det åt kursen.
  'course.noTeacher': {family: 'course', label: 'Ingen lärare', tone: 'warning'},

  'publishing.draft': {family: 'publishing', label: 'Utkast', tone: 'neutral'},
  'publishing.scheduled': {family: 'publishing', label: 'Schemalagd', tone: 'info', icon: PhCalendarBlank},
  'publishing.partial': {family: 'publishing', label: 'Delvis publicerad', tone: 'neutral'},
  'publishing.published': {family: 'publishing', label: 'Publicerad', tone: 'neutral'},

  'progress.todo': {family: 'progress', label: 'Att göra', tone: 'neutral'},
  'progress.locked': {family: 'progress', label: 'Låst', tone: 'neutral', icon: PhLock},
  'progress.done': {family: 'progress', label: 'Klar', tone: 'success', icon: PhCheckCircle},

  'submission.notSubmitted': {family: 'submission', label: 'Inte inlämnad', tone: 'neutral'},
  'submission.submitted': {family: 'submission', label: 'Inlämnad', tone: 'info', icon: PhPaperPlaneTilt},
  'submission.needsWork': {family: 'submission', label: 'Komplettera', tone: 'destructive', icon: PhWarningCircle},
  'submission.graded': {family: 'submission', label: 'Bedömd', tone: 'neutral'},
  // Läraren har markerat att eleven inte lämnade in. Skiljer sig från Inte inlämnad, som bara är ett läge före deadline.
  // Raderna i Att bedöma: om läraren bedömer en första inlämning eller en inlämning efter komplettering.
  'submission.first': {family: 'submission', label: 'Första inlämningen', tone: 'info'},
  'submission.again': {family: 'submission', label: 'Inlämnat igen', tone: 'destructive'},
  'submission.missed': {family: 'submission', label: 'Ej inlämnat', tone: 'destructive', icon: PhXCircle},

  'deadline.soon': {family: 'deadline', label: 'Deadline snart', tone: 'warning', icon: PhClock},
  'deadline.overdue': {family: 'deadline', label: 'Försenad', tone: 'destructive', icon: PhWarning},
} as const satisfies Record<string, StatusDefinition>

export type StatusKey = keyof typeof definitions

/** Ikonens färg för en ton, så att en ikon aldrig får en annan färg än märket för samma läge. */
export const toneTextClasses: Record<StatusTone, string> = {
  neutral: 'text-muted-foreground',
  info: 'text-info',
  warning: 'text-warning',
  destructive: 'text-destructive',
  success: 'text-success',
}

export const statuses: Record<StatusKey, StatusDefinition> = definitions

export function statusVariant(key: StatusKey): BadgeVariant {
  return toneVariants[statuses[key].tone]
}

/** Kursens läge (från courseStatus) som nyckel. */
export function courseStatusKey(status: keyof typeof courseStatusLabels): StatusKey {
  return `course.${status}`
}

/** Inlämningens läge som nyckel. Utan inlämning, eller när den inte är påbörjad, gäller Inte inlämnad. */
export function submissionStatusKey(
  status: Schema<'SubmissionStatus'> | null | undefined,
): StatusKey {
  switch (status) {
    case 'Submitted':
      return 'submission.submitted'
    case 'NeedsWork':
      return 'submission.needsWork'
    case 'Graded':
      return 'submission.graded'
    case 'NotSubmitted':
      return 'submission.missed'
    default:
      return 'submission.notSubmitted'
  }
}

/**
 * Deadlinens läge: Försenad när den har passerat, Deadline snart inom två dygn, annars inget.
 * Klara och inlämnade delar är aldrig försenade, och en vanlig deadline är ett datum i klartext.
 */
export function deadlineStatusKey(dueAt: string | null | undefined, done: boolean, now = new Date()): StatusKey | null {
  if (!dueAt || done) {
    return null
  }
  if (isOverdue(dueAt, now)) {
    return 'deadline.overdue'
  }
  return isDueSoon(dueAt, now)
    ? 'deadline.soon'
    : null
}

/**
 * Elevens läge för en del, från ett enda ställe. Inlämningen avgör före isCompleted, för att
 * isCompleted också är sann för en inlämnad men obedömd uppgift (inlämnat räcker för att låsa upp nästa).
 */
export function partStatusKey(item: {
  type: string
  isLocked: boolean
  isCompleted: boolean
  submissionStatus: Schema<'SubmissionStatus'> | null | undefined
  gradeLevel: 'Fail' | 'Pass' | 'Distinction' | null | undefined
}): StatusKey {
  if (item.isLocked) {
    return 'progress.locked'
  }
  if (item.type === 'Assignment') {
    if (item.submissionStatus === 'Submitted') {
      return 'submission.submitted'
    }
    if (item.submissionStatus === 'NeedsWork') {
      return 'submission.needsWork'
    }
    if (item.submissionStatus === 'NotSubmitted') {
      return 'submission.missed'
    }
    if (item.submissionStatus === 'Graded') {
      return item.gradeLevel === 'Fail'
        ? 'submission.graded'
        : 'progress.done'
    }
  }
  return item.isCompleted
    ? 'progress.done'
    : 'progress.todo'
}
