import {describe, expect, it} from 'vitest'

import type {Me} from './me'
import {actions, canDo, isStudentEverywhere, teachesAnywhere, type Action} from './permissions'

const teacherActions: Action[] = ['editContent', 'editCourse', 'grade', 'publishNews', 'manageAttachments']
const adminActions: Action[] = ['admin', 'manageCourse']

function user(role: Me['role'], courses: Me['courses']): Me {
  return {
    id: 'u1',
    name: 'Anna Lärare',
    email: 'anna@example.com',
    role,
    school: {id: 's1', name: 'Edugrade'},
    courses,
    groups: [],
    gitHubLogin: null,
    gitHubEnabled: false,
    gitHubInUse: false,
  }
}

// /api/me ger en admin inga kursroller av sig själv. Roller kommer av CourseTeacher och klasserna.
const admin = user('Admin', [])
const teacherOfA = user('Teacher', [{courseId: 'a', role: 'Teacher'}])
const student = user('Student', [{courseId: 'a', role: 'Student'}])

describe('canDo', () => {
  it('admin får administrera och ändra kursens fakta, i alla kurser', () => {
    for (const action of adminActions) {
      expect(canDo(admin, action)).toBe(true)
      expect(canDo(admin, action, 'a')).toBe(true)
      expect(canDo(admin, action, 'annan-kurs')).toBe(true)
    }
  })

  it('admin har ingen genväg in i undervisningen', () => {
    for (const action of teacherActions) {
      expect(canDo(admin, action)).toBe(false)
      expect(canDo(admin, action, 'a')).toBe(false)
    }
  })

  it('lärare i kurs A får ändra A men inte B', () => {
    for (const action of teacherActions) {
      expect(canDo(teacherOfA, action, 'a')).toBe(true)
      expect(canDo(teacherOfA, action, 'b')).toBe(false)
    }
  })

  it('lärare får bedöma utan kurs, men inte administrera eller ändra kursens fakta', () => {
    expect(canDo(teacherOfA, 'grade')).toBe(true)
    for (const action of adminActions) {
      expect(canDo(teacherOfA, action)).toBe(false)
      expect(canDo(teacherOfA, action, 'a')).toBe(false)
    }
  })

  it('elev får inget, varken i kursen eller utan', () => {
    for (const action of actions) {
      expect(canDo(student, action, 'a')).toBe(false)
      expect(canDo(student, action)).toBe(false)
    }
  })

  it('nekas allt medan användaren inte är hämtad', () => {
    for (const action of actions) {
      expect(canDo(null, action)).toBe(false)
      expect(canDo(null, action, 'a')).toBe(false)
    }
  })
})

describe('isStudentEverywhere', () => {
  it('är sant för den som varken är admin eller lärare i någon kurs', () => {
    expect(isStudentEverywhere(student)).toBe(true)
    expect(isStudentEverywhere(user('Teacher', []))).toBe(true)
    expect(isStudentEverywhere(teacherOfA)).toBe(false)
    expect(isStudentEverywhere(admin)).toBe(false)
    expect(isStudentEverywhere(null)).toBe(false)
  })
})

describe('teachesAnywhere', () => {
  it('följer lärarrollen i en kurs, inte den globala rollen', () => {
    expect(teachesAnywhere(teacherOfA)).toBe(true)
    expect(teachesAnywhere(admin)).toBe(false)
    expect(teachesAnywhere(user('Teacher', []))).toBe(false)
    expect(teachesAnywhere(null)).toBe(false)
  })
})
