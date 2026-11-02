import type {Schema} from '@/lib/api'

/** Skolrollerna (`User.Role`) med svenska namn, i den ordning de visas i val och listor. */
export const userRoles: {value: Schema<'UserRole'>, label: string}[] = [
  {value: 'Student', label: 'Elev'},
  {value: 'Teacher', label: 'Lärare'},
  {value: 'Admin', label: 'Admin'},
]

export const userRoleLabels: Record<Schema<'UserRole'>, string> = {Student: 'Elev', Teacher: 'Lärare', Admin: 'Admin'}
