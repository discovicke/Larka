import type {Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'

const gradeLabels: Record<Schema<'GradeLevel'>, string> = {
  Fail: 'IG',
  Pass: 'G',
  Distinction: 'VG',
}

const gradeNames: Record<Schema<'GradeLevel'>, string> = {
  Fail: 'Icke godkänt',
  Pass: 'Godkänt',
  Distinction: 'Väl godkänt',
}

const levelsByScale: Record<Schema<'GradeScale'>, Schema<'GradeLevel'>[]> = {
  None: [],
  PassFail: ['Fail', 'Pass'],
  PassFailDistinction: ['Fail', 'Pass', 'Distinction'],
}

/** Betygsbokstaven i listan, t.ex. "VG". */
export function gradeLabel(level: Schema<'GradeLevel'>): string {
  return gradeLabels[level]
}

/** Betygets namn i bedömningspanelen, t.ex. "Väl godkänt". */
export function gradeName(level: Schema<'GradeLevel'>): string {
  return gradeNames[level]
}

/** Betygsstegen som uppgiftens skala erbjuder. Tom när uppgiften bara har återkoppling. */
export function levelsFor(scale: Schema<'GradeScale'> | null): Schema<'GradeLevel'>[] {
  return scale
    ? [...levelsByScale[scale]]
    : []
}

/** Statusen i elevernas rad, t.ex. "Inlämnad mån 26 okt". */
export function statusLabel(submission: {status: Schema<'SubmissionStatus'>, submittedAt: string | null}): string {
  switch (submission.status) {
    case 'Submitted':
      return submission.submittedAt
        ? `Inlämnad ${formatDate(submission.submittedAt)}`
        : 'Inlämnad'
    case 'NeedsWork':
      return 'Komplettering'
    case 'Graded':
      return 'Bedömd'
    default:
      return 'Ej inlämnad'
  }
}

/** Filstorlek med svenskt decimalkomma, t.ex. "1,5 kB". */
export function formatSize(bytes: number): string {
  if (bytes < 1000) {
    return `${Math.round(bytes)} B`
  }
  if (bytes < 1_000_000) {
    return `${oneDecimal(bytes / 1000)} kB`
  }
  return `${oneDecimal(bytes / 1_000_000)} MB`
}

/** Testernas läge i GitHub-repot, t.ex. "Gröna". */
export function checkLabel(status: Schema<'CheckStatus'>): string {
  if (status === 'Green') {
    return 'Gröna'
  }
  return status === 'Red'
    ? 'Röda'
    : 'Väntar'
}

/** Färgen på testernas läge. */
export function checkClass(status: Schema<'CheckStatus'>): string {
  if (status === 'Green') {
    return 'text-emerald-700'
  }
  return status === 'Red'
    ? 'text-red-700'
    : 'text-muted-foreground'
}

function oneDecimal(value: number): string {
  return value
    .toFixed(1)
    .replace('.', ',')
}
