import type {Schema} from '@/lib/api'
import {gradeLabel} from '@/lib/submissions'

type Notification = Schema<'NotificationDto'>

export type InboxFilter = 'all' | 'unread' | 'grades' | 'news'

/** Vad en rad i inkorgen handlar om. Nyheter kommer från flödena, resten från notiserna. */
export type InboxKind = 'news' | 'grade' | 'needsWork' | 'deadline' | 'assignment'

export const kindLabels: Record<InboxKind, string> = {
  news: 'Nyhet',
  grade: 'Betyg',
  needsWork: 'Komplettering',
  deadline: 'Deadline',
  assignment: 'Ny uppgift',
}

/** Notiserna som blir egna rader. Nyhetsnotiserna visas på nyheten själv, som en oläst markering. */
const kindsByNotification: Partial<Record<Schema<'NotificationKind'>, InboxKind>> = {
  AssignmentPublished: 'assignment',
  DeadlineSoon: 'deadline',
  SubmissionNeedsWork: 'needsWork',
  SubmissionGraded: 'grade',
}

export function inboxKindOf(notification: Notification): InboxKind | undefined {
  return kindsByNotification[notification.kind]
}

/** Raden i listan: samma form för nyheter och notiser. */
export interface InboxRow {
  key: string
  kind: InboxKind
  at: string
  unread: boolean
  title: string
  where: string
  pinned: boolean
  // Nyhetens innehåll, eller notisen som raden kommer från.
  contentId?: string
  notification?: Notification
}

/** Rubriken på raden, t.ex. "Laboration: CI/CD har fått betyget G". */
export function notificationTitle(notification: Notification, kind: InboxKind): string {
  const title = notification.title ?? 'Borttaget innehåll'
  switch (kind) {
    case 'grade':
      return notification.level
        ? `${title} har fått betyget ${gradeLabel(notification.level)}`
        : `${title} har bedömts`
    case 'needsWork':
      return `${title} behöver kompletteras`
    case 'deadline':
      return `${title} ska lämnas in inom ett dygn`
    default:
      return `Ny uppgift: ${title}`
  }
}

export function notificationRow(notification: Notification, kind: InboxKind): InboxRow {
  return {
    key: `notification-${notification.id}`,
    kind,
    at: notification.createdAt,
    unread: notification.readAt === null,
    title: notificationTitle(notification, kind),
    where: notification.courseTitle ?? notification.groupName ?? '',
    pinned: false,
    notification,
  }
}

export function matchesFilter(row: InboxRow, filter: InboxFilter): boolean {
  switch (filter) {
    case 'unread':
      return row.unread
    case 'grades':
      return row.kind === 'grade' || row.kind === 'needsWork'
    case 'news':
      return row.kind === 'news'
    default:
      return true
  }
}

/** Nyast först. */
export function sortRows(rows: InboxRow[]): InboxRow[] {
  return [...rows].sort((left, right) => Date.parse(right.at) - Date.parse(left.at))
}
