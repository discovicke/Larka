import {ref} from 'vue'

import {api, type Schema} from '@/lib/api'

type Course = Schema<'CourseDto'>

// Kurserna i sidomenyn, på kurssidan och i brödsmulorna. AppLayout hämtar dem och alla delar samma lista.
// Visar alla kurser tills inloggning finns, sedan bara användarens (#80).
const courses = ref<Course[]>([])
const status = ref<'loading' | 'loaded' | 'failed'>('loading')

async function loadCourses() {
  status.value = 'loading'
  try {
    courses.value = (await api.GET('/api/courses')).data ?? []
    status.value = 'loaded'
  } catch {
    status.value = 'failed'
  }
}

function findCourse(id: string): Course | undefined {
  return courses.value.find((course) => course.id === id)
}

export function useCourses() {
  return {courses, status, loadCourses, findCourse}
}
