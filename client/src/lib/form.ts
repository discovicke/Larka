// Gör om värden från formulärfält till det API:et vill ha.

/** Tomma fält skickas som null i stället för "". */
export function orNull(value: string): string | null {
  return value.trim() === ''
    ? null
    : value
}

/** Gör om värdet från ett datetime-local-fält till ISO 8601 (UTC). */
export function isoOrNull(value: string): string | null {
  return value === ''
    ? null
    : new Date(value).toISOString()
}

/** Gör om ISO 8601 till värdet ett datetime-local-fält visar, i lokal tid. */
export function toDateTimeLocal(value: string | null | undefined): string {
  if (!value) {
    return ''
  }
  const date = new Date(value)
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 16)
}

/**
 * Första dagen (yyyy-mm-dd, lokal tid) som går att välja som elevens egen deadline: dagen för klassens sista
 * deadline eller idag, det som kommer sist. Ett tidigare datum gör ingen skillnad och nekas av backend.
 */
export function earliestPersonalDate(lastClassDeadline: string | null | undefined, now = new Date()): string {
  const today = toDateTimeLocal(now.toISOString()).slice(0, 10)
  const last = lastClassDeadline
    ? toDateTimeLocal(lastClassDeadline).slice(0, 10)
    : today
  return last > today
    ? last
    : today
}
