import {api, type Schema, unwrap} from './api'

/** Svaret från GET /api/me: den inloggade användaren och rollerna i kurserna. */
export type Me = Schema<'MeDto'>
export type MeRole = Schema<'UserRole'>
export type CourseRole = Schema<'GroupRole'>

/** Hämtar den inloggade användaren. Fel kastas som ApiError, som för alla anrop. */
export async function fetchMe(): Promise<Me> {
  return unwrap(await api.GET('/api/me'))
}
