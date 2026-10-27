import {PhBookOpenText, PhExam, PhNewspaper, PhPencilSimpleLine} from '@phosphor-icons/vue'
import type {Component} from 'vue'

import type {Schema} from '@/lib/api'

// "Typ av del" i formuläret. Övning och inlämning är båda uppgifter, men bara inlämningen har en betygsskala.
export type PartType = 'Theory' | 'Exercise' | 'Submission'

export interface PartTypeOption {
  value: PartType
  label: string
  description: string
  icon: Component
}

export const partTypes: PartTypeOption[] = [
  {value: 'Theory', label: 'Teori', description: 'Läsning, video', icon: PhBookOpenText},
  {value: 'Exercise', label: 'Övning', description: 'Utan betyg', icon: PhPencilSimpleLine},
  {value: 'Submission', label: 'Inlämning', description: 'Betygsätts', icon: PhExam},
]

/** Namn och ikon för innehåll i en lista. Nyheter finns inte i formuläret men kan ligga i en vecka. */
export function partOf(item: {type: Schema<'ContentType'>, gradeScale: Schema<'GradeScale'> | null}) {
  if (item.type === 'News') {
    return {label: 'Nyhet', icon: PhNewspaper}
  }
  return partTypes.find((partType) => partType.value === partTypeOf(item)) ?? partTypes[0]
}

export function partTypeOf(item: {type: Schema<'ContentType'>, gradeScale: Schema<'GradeScale'> | null}): PartType {
  if (item.type !== 'Assignment') {
    return 'Theory'
  }
  return item.gradeScale === 'None'
    ? 'Exercise'
    : 'Submission'
}

/** "1 del", "2 delar". */
export function countLabel(count: number, singular: string, plural: string): string {
  return `${count} ${count === 1
    ? singular
    : plural}`
}

/** "fre 23 okt", med klockslag: "mån 26 okt 07:00". */
export function formatDate(iso: string, withTime = false): string {
  const date = new Date(iso)
  const day = date
    .toLocaleDateString('sv-SE', {weekday: 'short', day: 'numeric', month: 'short'})
    .replaceAll('.', '')
  if (!withTime) {
    return day
  }
  return `${day} ${date.toLocaleTimeString('sv-SE', {hour: '2-digit', minute: '2-digit'})}`
}

export interface WeekPublishState {
  label: string
  class: string
  kind: 'published' | 'partial' | 'scheduled' | 'draft'
}

/**
 * Veckans badge. Veckans eget datum är en hård grind: innan det är passerat visas "Publiceras".
 * Efter det sammanfattas delarnas status, så att blandade veckor syns som "Delvis publicerad".
 */
export function weekPublishState(
  module: {
    publishAt?: string | null
    content: {status: Schema<'ContentStatus'>, publishAt: string}[]
  },
  now = new Date(),
): WeekPublishState | null {
  if (module.publishAt && new Date(module.publishAt) > now) {
    return {
      label: `Publiceras ${formatDate(module.publishAt, true)}`,
      class: 'bg-muted',
      kind: 'scheduled',
    }
  }

  if (module.content.length === 0) {
    return null
  }

  const live = module.content.filter((part) =>
    part.status === 'Published' && new Date(part.publishAt) <= now)
  if (live.length === module.content.length) {
    return {
      label: 'Publicerad',
      class: 'border-emerald-200 bg-emerald-50 text-emerald-700',
      kind: 'published',
    }
  }

  if (live.length > 0) {
    return {
      label: 'Delvis publicerad',
      class: 'border-amber-200 bg-amber-50 text-amber-700',
      kind: 'partial',
    }
  }

  const scheduled = module.content
    .filter((part) => part.status === 'Published' && new Date(part.publishAt) > now)
    .sort((left, right) => Date.parse(left.publishAt) - Date.parse(right.publishAt))
  if (scheduled.length > 0) {
    return {
      label: `Publiceras ${formatDate(scheduled[0].publishAt, true)}`,
      class: 'bg-muted',
      kind: 'scheduled',
    }
  }

  return {label: 'Utkast', class: '', kind: 'draft'}
}
