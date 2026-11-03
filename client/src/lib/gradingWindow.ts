import type {Schema} from '@/lib/api'
import {toDateTimeLocal} from '@/lib/form'

// Speglar backend/Application/Services/Content/GradingWindow.cs. Backend avgör och nekar, det här styr bara vad
// vyerna visar. En uppgift utan dueAt har ingen deadlinelogik: inlämning står öppen tills den bedömts och alla
// utfall är tillåtna.
// personalDueAt är en extra deadline för just den här eleven (på inlämningen). Den räknas med bland de andra.
export interface Deadlines {
  dueAt: string | null
  resubmitDueAt: string[]
  personalDueAt?: string | null
}

/** Alla elevens deadlines: dueAt, kompletteringarna och den personliga. Tom utan dueAt. */
function allDeadlines(item: Deadlines): string[] {
  if (!item.dueAt) {
    return []
  }
  return item.personalDueAt
    ? [item.dueAt, ...item.resubmitDueAt, item.personalDueAt]
    : [item.dueAt, ...item.resubmitDueAt]
}

/** Första av elevens deadlines (dueAt, kompletteringarna och den personliga) som ligger efter nu, annars null. */
export function nextDeadline(item: Deadlines, now = new Date()): string | null {
  return allDeadlines(item)
    .filter((value) => new Date(value) > now)
    .sort((first, second) => new Date(first).getTime() - new Date(second).getTime())[0] ?? null
}

/** Klassens sista deadline (dueAt eller sista kompletteringen), utan elevens egen. Null utan dueAt. */
export function lastClassDeadline(item: Deadlines): string | null {
  if (!item.dueAt) {
    return null
  }
  return [item.dueAt, ...item.resubmitDueAt]
    .sort((first, second) => new Date(second).getTime() - new Date(first).getTime())[0]
}

/**
 * Första dagen (yyyy-mm-dd, lokal tid) som går att välja som elevens egna deadline: dagen för klassens sista
 * deadline eller idag, det som kommer sist. Ett tidigare datum gör ingen skillnad och nekas av backend.
 */
export function earliestPersonalDate(lastDeadline: string | null, now = new Date()): string {
  const today = toDateTimeLocal(now.toISOString()).slice(0, 10)
  const last = lastDeadline
    ? toDateTimeLocal(lastDeadline).slice(0, 10)
    : today
  return last > today
    ? last
    : today
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

/**
 * Automatiskt IG: uppgiften har en deadline, ingen deadline återstår och eleven har inte lämnat in (NotStarted)
 * eller inte kompletterat (NeedsWork). Härleds alltid och sparas aldrig. Elevens personliga deadline
 * räknas med i nextDeadline, så att regeln fortfarande bor på ett ställe.
 */
export function isAutoFail(item: Deadlines, status: Schema<'SubmissionStatus'>, now = new Date()): boolean {
  return Boolean(item.dueAt)
    && !deadlineRemains(item, now)
    && (status === 'NotStarted' || status === 'NeedsWork')
}

/** Texten på ett automatiskt IG: det som saknades avgör förklaringen. */
export function autoFailLabel(status: Schema<'SubmissionStatus'>): string {
  return status === 'NeedsWork'
    ? 'IG · Kompletteringen lämnades inte in'
    : 'IG · Ingen inlämning'
}
