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
