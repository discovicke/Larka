import {api, type Schema} from './api'

type GroupRole = Schema<'GroupRole'>

export async function fetchGroups() {
  const {data} = await api.GET('/api/groups')
  return data ?? []
}

/** Kurserna som gruppen läser. */
export async function fetchGroupCourses(groupId: string) {
  const {data} = await api.GET('/api/groups/{id}/courses', {params: {path: {id: groupId}}})
  return data ?? []
}

export async function fetchGroupMembers(groupId: string) {
  const {data} = await api.GET('/api/groups/{id}/members', {params: {path: {id: groupId}}})
  return data ?? []
}

/** Flödet som är publicerat till gruppen, valfritt bara en typ (t.ex. 'news'). */
export async function fetchGroupContent(groupId: string, type?: 'news') {
  const {data} = await api.GET('/api/groups/{id}/content', {
    params: {path: {id: groupId}, query: {type}},
  })
  return data ?? []
}

export async function linkCourseToGroup(groupId: string, courseId: string) {
  await api.POST('/api/groups/{id}/courses/{courseId}', {params: {path: {id: groupId, courseId}}})
}

export async function unlinkCourseFromGroup(groupId: string, courseId: string) {
  await api.DELETE('/api/groups/{id}/courses/{courseId}', {params: {path: {id: groupId, courseId}}})
}

export async function addGroupMember(groupId: string, userId: string, role: GroupRole) {
  await api.POST('/api/groups/{id}/members', {
    params: {path: {id: groupId}},
    body: {userId, role},
  })
}

export async function changeMemberRole(groupId: string, userId: string, role: GroupRole) {
  await api.PUT('/api/groups/{id}/members/{userId}', {
    params: {path: {id: groupId, userId}},
    body: {role},
  })
}

export async function removeGroupMember(groupId: string, userId: string) {
  await api.DELETE('/api/groups/{id}/members/{userId}', {params: {path: {id: groupId, userId}}})
}
