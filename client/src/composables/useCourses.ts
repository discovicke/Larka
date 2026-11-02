import {ref} from 'vue'

import {type Schema} from '@/lib/api'
import {fetchCourses} from '@/lib/coursesApi'

type Course = Schema<'CourseDto'>

// Kurserna i sidomenyn, på kurssidan och i brödsmulorna. AppLayout hämtar dem och alla delar samma lista.
// Servern ger admin hela skolan och alla andra bara kurserna deras grupper läser (#247).
const courses = ref<Course[]>([])
const status = ref<'loading' | 'loaded' | 'failed'>('loading')

async function loadCourses() {
  status.value = 'loading'
  try {
    courses.value = await fetchCourses()
    status.value = 'loaded'
  } catch {
    status.value = 'failed'
  }
}

function findCourse(id: string): Course | undefined {
  return courses.value.find((course) => course.id === id)
}

/** Ersätter en kurs i listan, t.ex. efter att läraren sparat ändringar på fliken Om. */
function updateCourse(updated: Course) {
  const index = courses.value.findIndex((course) => course.id === updated.id)
  if (index >= 0) {
    courses.value[index] = updated
  }
}

/** Lägger till en nyskapad kurs, så att sidomenyn och Alla grupper visar den utan omladdning. */
/** Tar bort en borttagen kurs ur listan. */
function removeCourse(id: string) {
  courses.value = courses.value.filter((course) => course.id !== id)
}

export function useCourses() {
  return {courses, status, loadCourses, findCourse, updateCourse, removeCourse}
}
