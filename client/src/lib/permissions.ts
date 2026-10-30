import type {Me} from './me'

/** Alla åtgärder, för tester och för den som vill lista dem. */
export const actions = [
  'editContent',
  'editCourse',
  'grade',
  'publishNews',
  'manageAttachments',
  'readSubmissions',
  'manageCourse',
  'admin',
] as const

/** Det användaren kan göra i appen. */
export type Action = (typeof actions)[number]

// Admin bestämmer vem som undervisar vad: skolan och kursens fakta. Allt annat hör till kursens lärare.
const adminActions: Action[] = ['admin', 'manageCourse']

/**
 * Reglerna bakom can() i useCurrentUser. Allt bygger på relationen till kursen, inte på den globala rollen.
 * - Admin får 'admin' och 'manageCourse': skolans inställningar, och kursens titel, kod, datum och poäng.
 * - Att redigera, publicera och bedöma kräver lärarrollen i just den kursen. Admin har ingen genväg dit.
 * - Att läsa inlämningar och betyg får läraren och mentorn i kursen.
 * - Utan kurs (t.ex. sidomenyns Att bedöma) räcker lärarrollen i någon kurs.
 * Så länge användaren inte är hämtad (null) nekas allt, så att inget hinner visas innan svaret finns.
 */
export function canDo(me: Me | null, action: Action, courseId?: string): boolean {
  if (!me) {
    return false
  }
  if (adminActions.includes(action)) {
    return me.role === 'Admin'
  }
  const roles = (me.courses ?? [])
    .filter((course) => courseId === undefined || course.courseId === courseId)
    .map((course) => course.role)
  if (action === 'readSubmissions') {
    return roles.some((role) => role === 'Teacher' || role === 'Mentor')
  }
  return roles.includes('Teacher')
}

/** Sant när användaren är lärare i minst en kurs. */
export function teachesAnywhere(me: Me | null): boolean {
  return canDo(me, 'grade')
}

/** Sant när användaren varken är admin eller lärare i någon kurs: eleven, och mentorn som bara läser. */
export function isStudentEverywhere(me: Me | null): boolean {
  return me !== null && me.role !== 'Admin' && !teachesAnywhere(me)
}

/** Sant när användaren bara är mentor: har mentorrollen i en kurs men varken lärar- eller elevrollen. */
export function isMentorOnly(me: Me | null): boolean {
  const roles = (me?.courses ?? []).map((course) => course.role)
  return roles.includes('Mentor') && !roles.includes('Teacher') && !roles.includes('Student')
}
