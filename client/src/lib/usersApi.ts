import {api} from './api'

/** Alla användare i skolan, för listor där man väljer lärare, mentor eller elev. */
export async function fetchUsers() {
  const {data} = await api.GET('/api/users')
  return data ?? []
}
