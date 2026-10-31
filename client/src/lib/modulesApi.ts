import {api} from './api'

/** Kursens veckor med delarna, så som läraren ser dem. */
export async function fetchCourseModules(courseId: string) {
  const {data} = await api.GET('/api/courses/{id}/modules', {params: {path: {id: courseId}}})
  return data ?? []
}

/** Kursens veckor med delarna, så som eleven ser dem (med elevens egen status). */
export async function fetchStudentModules(courseId: string) {
  const {data} = await api.GET('/api/courses/{courseId}/student-modules', {params: {path: {courseId}}})
  return data ?? []
}
