import {api, type Schema} from './api'

export type StudentHelpStatus = Schema<'StudentHelpStatusDto'>

export async function fetchStudentHelp(linkId: string): Promise<StudentHelpStatus> {
  const {data} = await api.GET('/api/links/{id}/help', {params: {path: {id: linkId}}})
  return data!
}

export async function requestStudentHelp(
  linkId: string,
  message?: string | null,
): Promise<StudentHelpStatus> {
  const {data} = await api.POST('/api/links/{id}/help', {
    params: {path: {id: linkId}},
    body: {message: message?.trim() || null},
  })
  return data!
}

export async function withdrawStudentHelp(linkId: string): Promise<void> {
  await api.DELETE('/api/links/{id}/help', {params: {path: {id: linkId}}})
}

export async function resolveStudentHelp(linkId: string, studentId: string): Promise<void> {
  await api.POST('/api/links/{id}/help/{studentId}/resolve', {
    params: {path: {id: linkId, studentId}},
  })
}
