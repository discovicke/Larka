import type {Me} from './me'

/** Det användaren kan göra i appen. */
export type Action = 'editContent' | 'editCourse' | 'grade' | 'publishNews' | 'manageAttachments' | 'admin'

/**
 * Reglerna bakom can() i useCurrentUser.
 * Admin får allt. Övriga åtgärder kräver att man är lärare, och för en kurs krävs lärarrollen i just den kursen.
 * Så länge användaren inte är hämtad (null) nekas allt, så att inget hinner visas innan svaret finns.
 */
export function canDo(me: Me | null, action: Action, courseId?: string): boolean {
  if (!me) {
    return false
  }
  if (me.role === 'Admin') {
    return true
  }
  if (action === 'admin') {
    return false
  }
  if (courseId !== undefined) {
    return (me.courses ?? []).some((course) => course.courseId === courseId && course.role === 'Teacher')
  }
  // Utan kurs, t.ex. sidomenyns Att bedöma, räcker det att man är lärare någonstans.
  return me.role === 'Teacher'
}

/** Sant när användaren inte har någon lärarroll alls, varken i skolan eller i en kurs. */
export function isStudentEverywhere(me: Me | null): boolean {
  return me !== null
    && me.role !== 'Admin'
    && me.role !== 'Teacher'
    && !(me.courses ?? []).some((course) => course.role === 'Teacher')
}
