import type {Me, MeRole} from '@/lib/me'

/**
 * En inloggad användare i testerna. Lärarrollen gäller kurserna man anger, så att mockarna kan
 * svara som en lärare i sina egna kurser och som elev i andras.
 */
export function meAs(
  role: MeRole,
  courses: string[] = [],
  name = 'Anna Lärare',
  gitHubLogin: string | null = null,
  gitHubEnabled = false,
  gitHubInUse = false,
): Me {
  return {
    id: 'u-me',
    name,
    email: 'anna@example.com',
    role,
    school: {id: 's1', name: 'Edugrade'},
    courses: courses.map((courseId) => ({
      courseId,
      role: role === 'Student'
        ? 'Student'
        : 'Teacher',
    })),
    groups: [],
    gitHubLogin,
    gitHubEnabled,
    gitHubInUse,
  }
}

/**
 * En mentor. Den globala rollen är Teacher, som hos de seedade mentorerna, men i klassens kurser är rollen Mentor.
 * Mentorn får därför läsa kursen och inlämningarna men inte bedöma.
 */
export function mentorAs(courses: string[] = [], name = 'Malin Ekström'): Me {
  return {
    ...meAs('Teacher', courses, name),
    courses: courses.map((courseId) => ({courseId, role: 'Mentor'})),
  }
}
