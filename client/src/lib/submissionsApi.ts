import {api} from './api'

/** Inlämningarna på en uppgift. Eleven får bara sin egen, läraren alla. */
export async function fetchLinkSubmissions(linkId: string) {
  const {data} = await api.GET('/api/links/{id}/submissions', {params: {path: {id: linkId}}})
  return data ?? []
}

/** Markerar alla som inte lämnat in som Ej inlämnat, efter huvuddeadlinen. Returnerar hur många det blev. */
export async function markAllNotSubmitted(linkId: string): Promise<number> {
  const {data} = await api.POST('/api/links/{id}/submissions/mark-not-submitted', {params: {path: {id: linkId}}})
  return data?.count ?? 0
}

/** Beskedet efter att läraren markerat alla. */
export function markedNotSubmittedMessage(count: number): string {
  if (count === 0) {
    return 'Ingen behövde markeras som ej inlämnat.'
  }
  return count === 1
    ? '1 elev markerades som ej inlämnat.'
    : `${count} elever markerades som ej inlämnat.`
}
