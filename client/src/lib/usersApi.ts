import {api, type Schema} from './api'

/** Användarna i skolan som inte är spärrade, för listor där man väljer lärare, mentor eller elev. */
export async function fetchUsers() {
  return (await fetchAllUsers()).filter((user) => !user.deactivatedAt)
}

/** Alla användare i skolan, också de spärrade. För sidan Användare. */
export async function fetchAllUsers() {
  const {data} = await api.GET('/api/users')
  return data ?? []
}

/** Spärrar användaren, eller släpper spärren. Bara admin, och inte sig själv. */
export async function setUserDeactivated(id: string, deactivate: boolean) {
  const {data} = deactivate
    ? await api.POST('/api/users/{id}/deactivate', {params: {path: {id}}})
    : await api.POST('/api/users/{id}/reactivate', {params: {path: {id}}})
  return data!
}

/** Tar bort kopplingen till användarens Microsoft-konto. Nästa inloggning kopplar hen igen på e-posten. */
export async function unlinkMicrosoft(id: string) {
  const {data} = await api.DELETE('/api/users/{id}/microsoft', {params: {path: {id}}})
  return data!
}

/** Ändrar användarens skolroll. Bara admin, och inte den egna rollen. */
export async function updateUserRole(id: string, role: Schema<'UserRole'>) {
  const {data} = await api.PUT('/api/users/{id}/role', {params: {path: {id}}, body: {role}})
  return data!
}
