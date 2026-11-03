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
