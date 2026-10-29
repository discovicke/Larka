import type {Schema} from '@/lib/api'

/** Gruppens typ på svenska, som den visas i listor och på gruppsidan. */
export const groupTypeLabels: Record<Schema<'GroupType'>, string> = {
  Class: 'Klass',
  Committee: 'Kommitté',
  Other: 'Grupp',
}
