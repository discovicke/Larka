import {api, type Schema, unwrap} from './api'

export async function fetchContent(contentId: string) {
  const {data} = await api.GET('/api/content/{id}', {params: {path: {id: contentId}}})
  return data ?? null
}

export async function fetchAttachments(contentId: string) {
  const {data} = await api.GET('/api/content/{id}/attachments', {params: {path: {id: contentId}}})
  return data ?? []
}

export async function setContentStatus(contentId: string, status: Schema<'ContentStatus'>) {
  await api.PUT('/api/content/{id}/status', {params: {path: {id: contentId}}, body: {status}})
}

export async function deleteContentLink(contentId: string, linkId: string) {
  await api.DELETE('/api/content/{id}/links/{linkId}', {params: {path: {id: contentId, linkId}}})
}

export async function updateContent(contentId: string, body: Schema<'UpdateContentDto'>) {
  await api.PUT('/api/content/{id}', {params: {path: {id: contentId}}, body})
}

export async function updateContentLink(contentId: string, linkId: string, body: Schema<'UpdateContentLinkDto'>) {
  await api.PUT('/api/content/{id}/links/{linkId}', {params: {path: {id: contentId, linkId}}, body})
}

export async function createContentWithLink(body: Schema<'CreateContentWithLinkDto'>) {
  return unwrap(await api.POST('/api/content/with-link', {body}))
}
