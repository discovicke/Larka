export interface Option {
  value: string
  label: string
}

/** Det som visas under ett formulär efter att man tryckt på knappen. */
export type FormResult
  = | {kind: 'preview', body: unknown}
    | {kind: 'response', status: number, body: unknown}
    | {kind: 'error', status?: number, message: string}

export const userRoles = ['Admin', 'Teacher', 'Student'] as const
export const groupTypes = ['Class', 'Committee', 'Other'] as const
export const groupRoles = ['Student', 'Teacher', 'Mentor'] as const
export const contentTypes = ['News', 'Theory', 'Assignment'] as const
export const contentStatuses = ['Draft', 'Published'] as const
export const submissionModes = ['None', 'File', 'Repo', 'FileOrRepo'] as const

/** Tomma fält skickas som null i stället för "". */
export function orNull(value: string): string | null {
  return value.trim() === ''
    ? null
    : value
}

export function numberOrNull(value: string | number): number | null {
  return value === ''
    ? null
    : Number(value)
}

/** Gör om värdet från ett datetime-local-fält till ISO 8601 (UTC). */
export function isoOrNull(value: string): string | null {
  return value === ''
    ? null
    : new Date(value).toISOString()
}
