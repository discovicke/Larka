import type {Schema} from '@/lib/api'

/** Backendens deadlinetillstånd med neutrala värden: öppet, inget automatiskt IG och alla utfall tillåtna. */
export function deadlineState(overrides: Partial<Schema<'DeadlineStateDto'>> = {}): Schema<'DeadlineStateDto'> {
  return {
    nextDeadline: null,
    lastClassDeadline: null,
    autoFail: false,
    canHandIn: true,
    handInUntil: null,
    handInClosed: null,
    allowsFail: true,
    allowsNeedsWork: true,
    ...overrides,
  }
}
