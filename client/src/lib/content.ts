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
