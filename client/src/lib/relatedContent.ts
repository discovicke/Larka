import {api, type Schema} from './api'

export type RelatedLink = Schema<'RelatedLinkDto'>

export async function fetchRelatedLinks(linkId: string): Promise<RelatedLink[]> {
  const {data} = await api.GET('/api/links/{id}/related', {params: {path: {id: linkId}}})
  return data ?? []
}
