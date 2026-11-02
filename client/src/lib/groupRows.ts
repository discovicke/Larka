import type {RouteLocationRaw} from 'vue-router'

import type {Schema} from '@/lib/api'
import type {CourseStatus} from '@/lib/courseStatus'

/** En rad i Alla grupper: skolan, en grupp eller en kurs. */
export interface GroupRow {
  key: string
  kind: 'school' | 'group' | 'course'
  name: string
  code: string
  type: string
  period: string
  status: CourseStatus | null
  /** Raden hör hemma under Avslutade: en avslutad kurs, eller en klass vars kurser alla är avslutade. */
  finished: boolean
  to?: RouteLocationRaw
  group?: Schema<'GroupDto'>
  course?: Schema<'CourseDto'>
}
