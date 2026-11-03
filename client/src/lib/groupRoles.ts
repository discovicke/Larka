import type {Schema} from '@/lib/api'

/** Rollerna i en grupp med lärare först, som de visas på gruppsidan och i valen. */
export const groupRoles: {value: Schema<'GroupRole'>, label: string}[] = [
  {value: 'Teacher', label: 'Lärare'},
  {value: 'Student', label: 'Elev'},
]
