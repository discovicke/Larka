import type {Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'

const gradeLabels: Record<Schema<'GradeLevel'>, string> = {
  Fail: 'IG',
  Pass: 'G',
  Distinction: 'VG',
}

const gradeNames: Record<Schema<'GradeLevel'>, string> = {
  Fail: 'Icke godkänd',
  Pass: 'Godkänd',
  Distinction: 'Väl godkänd',
}

const levelsByScale: Record<Schema<'GradeScale'>, Schema<'GradeLevel'>[]> = {
  None: [],
  PassFail: ['Fail', 'Pass'],
  PassFailDistinction: ['Fail', 'Pass', 'Distinction'],
}

/** Betygsbokstaven i den runda stämpeln, t.ex. "VG". Stämpeln får hela namnet som etikett, se gradeName. */
export function gradeLabel(level: Schema<'GradeLevel'>): string {
  return gradeLabels[level]
}

/** Badge-varianten för betygsstämpeln: G i grönt, VG i gult i ljust läge (volt i mörkt) och IG i grått. */
export function gradeBadgeVariant(level: Schema<'GradeLevel'>): 'gradePass' | 'gradeDistinction' | 'gradeFail' {
  if (level === 'Distinction') {
    return 'gradeDistinction'
  }
  return level === 'Pass'
    ? 'gradePass'
    : 'gradeFail'
}

/** Betygets namn i klartext, t.ex. "Väl godkänd". Används i all löpande text och som etikett på stämpeln. */
export function gradeName(level: Schema<'GradeLevel'>): string {
  return gradeNames[level]
}

/** Betygsskalan i klartext, t.ex. "Icke godkänd / Godkänd / Väl godkänd". Tom när det inte finns någon skala. */
export function gradeScaleName(scale: Schema<'GradeScale'> | null): string {
  const levels = levelsFor(scale)
  return levels.map((level) => gradeNames[level]).join(' / ')
}

/** Betygsskalan med bokstäverna, t.ex. "IG / G / VG", för smala listor. Hela namnen står i gradeScaleName. */
export function gradeScaleShort(scale: Schema<'GradeScale'> | null): string {
  return levelsFor(scale).map((level) => gradeLabels[level]).join(' / ')
}

/** Betygsstegen som uppgiftens skala erbjuder. Tom när uppgiften bara har återkoppling. */
export function levelsFor(scale: Schema<'GradeScale'> | null): Schema<'GradeLevel'>[] {
  return scale
    ? [...levelsByScale[scale]]
    : []
}

/** Vem som bedömde och när, t.ex. "Bedömd av Tomas Berg ons 28 okt". Utan lärarens namn bara datumet. */
export function gradedLabel(grade: Pick<Schema<'GradeDto'>, 'gradedAt' | 'gradedByName'>, withTime = false): string {
  const date = formatDate(grade.gradedAt, withTime)
  return grade.gradedByName
    ? `Bedömd av ${grade.gradedByName} ${date}`
    : `Bedömd ${date}`
}

/**
 * Statusen i elevernas rad, t.ex. "Inlämnad mån 26 okt" eller "Bedömd av Tomas Berg". Med autoFail visas det
 * automatiska IG:t, som ingen lärare har satt.
 */
export function statusLabel(
  submission: {
    status: Schema<'SubmissionStatus'>
    submittedAt: string | null
    grade?: Pick<Schema<'GradeDto'>, 'gradedByName'> | null
  },
  autoFail = false,
): string {
  if (autoFail) {
    return autoFailLabel(submission.status)
  }
  switch (submission.status) {
    case 'Submitted':
      return submission.submittedAt
        ? `Inlämnad ${formatDate(submission.submittedAt)}`
        : 'Inlämnad'
    case 'NeedsWork':
      return 'Komplettering'
    case 'Graded':
      return submission.grade?.gradedByName
        ? `Bedömd av ${submission.grade.gradedByName}`
        : 'Bedömd'
    default:
      return 'Inte inlämnad'
  }
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
    return 'text-success'
  }
  return status === 'Red'
    ? 'text-destructive'
    : 'text-muted-foreground'
}

const submissionModeLabels: Record<string, string> = {
  Text: 'text',
  File: 'filer',
  Repo: 'GitHub-repo',
  OnSite: 'visas på plats',
}

/** Inlämningssätten som text, till exempel "text, filer". */
export function handInLabel(modes: string[]): string {
  return modes.map((mode) => submissionModeLabels[mode] ?? mode).join(', ')
}

/** Den som står näst i bedömningskön: inlämnade, sorterade på namn som i listan, utom den som är öppen. */
export function nextInQueue<T extends {id: string, studentName: string, status: Schema<'SubmissionStatus'>}>(
  list: T[],
  current: {id: string, studentName: string},
): T | null {
  const others = list
    .filter((candidate) => candidate.status === 'Submitted' && candidate.id !== current.id)
    .sort((left, right) => left.studentName.localeCompare(right.studentName, 'sv'))
  return others.find((candidate) => candidate.studentName.localeCompare(current.studentName, 'sv') > 0)
    ?? others[0]
    ?? null
}

/** Texten på ett automatiskt IG: det som saknades avgör förklaringen. */
export function autoFailLabel(status: Schema<'SubmissionStatus'>): string {
  return status === 'NeedsWork'
    ? 'IG · Kompletteringen lämnades inte in'
    : 'IG · Ingen inlämning'
}
