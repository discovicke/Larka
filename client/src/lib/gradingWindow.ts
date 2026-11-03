import type {Schema} from '@/lib/api'

// Speglar backend/Application/Services/Content/GradingWindow.cs. Backend avgör och nekar, det här styr bara vad
// vyerna visar. En uppgift utan dueAt har ingen deadlinelogik: inlämning står öppen tills den bedömts och alla
// utfall är tillåtna.
interface Deadlines {
  dueAt: string | null
  resubmitDueAt: string[]
}

/** Första av dueAt och kompletteringsdeadlinerna som ligger efter nu, annars null. */
export function nextDeadline(item: Deadlines, now = new Date()): string | null {
  if (!item.dueAt) {
    return null
  }
  return [item.dueAt, ...item.resubmitDueAt]
    .filter((value) => new Date(value) > now)
    .sort((first, second) => new Date(first).getTime() - new Date(second).getTime())[0] ?? null
}

/** Sant om någon deadline återstår. Utan dueAt finns ingen. */
export function deadlineRemains(item: Deadlines, now = new Date()): boolean {
  return nextDeadline(item, now) !== null
}

export interface HandInWindow {
  /** Får eleven lämna in eller ändra just nu. */
  open: boolean
  /** Deadlinen som stänger fönstret, null utan deadline eller när det är stängt. */
  until: string | null
  /** Varför det är stängt: låst efter deadline, eller ingen deadline kvar. */
  closed: 'locked' | 'closed' | null
}

/** När eleven får lämna in: se tillståndstabellen i docs. En bedömd inlämning är alltid stängd. */
export function handInWindow(
  item: Deadlines,
  status: Schema<'SubmissionStatus'>,
  now = new Date(),
): HandInWindow {
  if (status === 'Graded') {
    return {open: false, until: null, closed: null}
  }
  if (!item.dueAt) {
    return {open: true, until: null, closed: null}
  }
  if (status === 'Submitted') {
    return new Date(item.dueAt) > now
      ? {open: true, until: item.dueAt, closed: null}
      : {open: false, until: null, closed: 'locked'}
  }
  const until = nextDeadline(item, now)
  return until
    ? {open: true, until, closed: null}
    : {open: false, until: null, closed: 'closed'}
}

/** IG får först sättas när ingen deadline återstår. */
export function allowsFail(item: Deadlines, now = new Date()): boolean {
  return !item.dueAt || !deadlineRemains(item, now)
}

/** Komplettera ger eleven en ny chans, så det kräver att en deadline återstår. */
export function allowsNeedsWork(item: Deadlines, now = new Date()): boolean {
  return !item.dueAt || deadlineRemains(item, now)
}

/** Ej inlämnat för alla på en gång kräver en huvuddeadline som har gått ut. Utan dueAt finns ingen. */
export function mayMarkAllNotSubmitted(item: Deadlines, now = new Date()): boolean {
  return Boolean(item.dueAt) && new Date(item.dueAt as string) <= now
}
