import {api} from './api'

/** Inlämningarna på en uppgift. Eleven får bara sin egen, läraren alla. */
export async function fetchLinkSubmissions(linkId: string) {
  const {data} = await api.GET('/api/links/{id}/submissions', {params: {path: {id: linkId}}})
  return data ?? []
}

/** Läraren ger eleven en extra deadline, eller tar bort den med null. Returnerar elevens uppdaterade inlämning. */
export async function setPersonalDueAt(linkId: string, studentId: string, dueAt: string | null) {
  const {data} = await api.PUT('/api/links/{id}/students/{studentId}/personal-due-at', {
    params: {path: {id: linkId, studentId}},
    body: {dueAt},
  })
  return data
}
