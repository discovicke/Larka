import {api} from './api'

/** Inlämningarna på en uppgift. Eleven får bara sin egen, läraren alla. */
export async function fetchLinkSubmissions(linkId: string) {
  const {data} = await api.GET('/api/links/{id}/submissions', {params: {path: {id: linkId}}})
  return data ?? []
}
