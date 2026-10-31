import {api, type Schema, unwrap} from './api'

/** Kurserna användaren ser: admin hela skolan, alla andra kurserna deras grupper läser. */
export async function fetchCourses() {
  const {data} = await api.GET('/api/courses')
  return data ?? []
}

export async function createCourse(body: Schema<'CreateCourseDto'>) {
  return unwrap(await api.POST('/api/courses', {body}))
}

export async function updateCourse(courseId: string, body: Schema<'UpdateCourseDto'>) {
  return unwrap(await api.PUT('/api/courses/{id}', {params: {path: {id: courseId}}, body}))
}

export async function addCourseTeacher(courseId: string, userId: string) {
  return unwrap(await api.POST('/api/courses/{id}/teachers', {
    params: {path: {id: courseId}},
    body: {userId},
  }))
}

/** Kursens flöde, valfritt bara en typ (t.ex. 'news'). */
export async function fetchCourseContent(courseId: string, type?: 'news') {
  const {data} = await api.GET('/api/courses/{id}/content', {
    params: {path: {id: courseId}, query: {type}},
  })
  return data ?? []
}

/** Lärarens kurser med det som väntar på bedömning. */
export async function fetchTeaching() {
  return unwrap(await api.GET('/api/me/teaching'))
}
