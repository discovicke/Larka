import {api, type Schema} from './api'
import type {GroupForm} from './createContent'

export type AssignmentGroup = Schema<'AssignmentGroupDto'>

export async function fetchAssignmentGroups(linkId: string): Promise<AssignmentGroup[]> {
  const {data} = await api.GET('/api/links/{id}/groups', {params: {path: {id: linkId}}})
  return data ?? []
}

export async function updateAssignmentGroups(linkId: string, groups: GroupForm[]): Promise<AssignmentGroup[]> {
  const {data} = await api.PUT('/api/links/{id}/groups', {
    params: {path: {id: linkId}},
    body: {
      groups: groups.map((group) => ({
        id: group.id ?? null,
        name: group.name,
        studentIds: group.studentIds,
      })),
    },
  })
  return data ?? []
}
