// Exempeldata tills backend har inloggning och listor för skolor, användare och kurser (#32).
// Byts mot API-anrop när de finns.

export interface ExampleCourse {
  id: string
  code: string
  title: string
  period: string
  // Färgen på rutan framför kursen i sidomenyn.
  marker: 'active' | 'muted'
}

export const exampleSchool = {name: 'Edugrade'}

export const exampleUser = {name: 'Placeholder', initials: 'PH', role: 'Lärare'}

export const exampleCounts = {inbox: 3, grading: 5}

export const exampleCourses: ExampleCourse[] = [
  {
    id: 'devops',
    code: '.NET25_DEVOPS',
    title: 'DevOps',
    period: '12 okt – 13 nov 2026 · 25 p · Vecka 2 av 5',
    marker: 'active',
  },
  {
    id: 'lia',
    code: 'LIA',
    title: 'LIA – Lärande i arbete',
    period: 'Startar 2 nov',
    marker: 'muted',
  },
]

export function findCourse(courseId: string): ExampleCourse | undefined {
  return exampleCourses.find((course) => course.id === courseId)
}
