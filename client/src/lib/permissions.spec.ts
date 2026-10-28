import {describe, expect, it} from 'vitest'

import type {Me} from './me'
import {canDo, isStudentEverywhere, type Action} from './permissions'

const allActions: Action[] = ['editContent', 'editCourse', 'grade', 'publishNews', 'manageAttachments', 'admin']

function user(role: Me['role'], courses: Me['courses']): Me {
  return {
    id: 'u1',
    name: 'Anna Lärare',
    email: 'anna@example.com',
    role,
    school: {id: 's1', name: 'Edugrade'},
    courses,
  }
}

const admin = user('Admin', [{courseId: 'a', role: 'Teacher'}])
const teacherOfA = user('Teacher', [{courseId: 'a', role: 'Teacher'}])
const student = user('Student', [{courseId: 'a', role: 'Student'}])
const mentor = user('Student', [{courseId: 'a', role: 'Mentor'}])

describe('canDo', () => {
  it('admin får allt, både med och utan kurs', () => {
    for (const action of allActions) {
      expect(canDo(admin, action)).toBe(true)
      expect(canDo(admin, action, 'a')).toBe(true)
      expect(canDo(admin, action, 'annan-kurs')).toBe(true)
    }
  })

  it('lärare i kurs A får ändra A men inte B', () => {
    expect(canDo(teacherOfA, 'editContent', 'a')).toBe(true)
    expect(canDo(teacherOfA, 'grade', 'a')).toBe(true)
    expect(canDo(teacherOfA, 'publishNews', 'a')).toBe(true)
    expect(canDo(teacherOfA, 'manageAttachments', 'a')).toBe(true)

    expect(canDo(teacherOfA, 'editContent', 'b')).toBe(false)
    expect(canDo(teacherOfA, 'grade', 'b')).toBe(false)
  })

  it('lärare utan kurs får bedöma men inte administrera', () => {
    expect(canDo(teacherOfA, 'grade')).toBe(true)
    expect(canDo(teacherOfA, 'admin')).toBe(false)
  })

  it('elev får inget, varken i kursen eller utan', () => {
    for (const action of allActions) {
      expect(canDo(student, action, 'a')).toBe(false)
      expect(canDo(student, action)).toBe(false)
    }
  })

  it('mentor räknas inte som lärare i kursen', () => {
    expect(canDo(mentor, 'editContent', 'a')).toBe(false)
    expect(canDo(mentor, 'grade', 'a')).toBe(false)
  })

  it('nekas allt medan användaren inte är hämtad', () => {
    for (const action of allActions) {
      expect(canDo(null, action)).toBe(false)
      expect(canDo(null, action, 'a')).toBe(false)
    }
  })
})

describe('isStudentEverywhere', () => {
  it('är sant bara för den som inte har någon lärarroll', () => {
    expect(isStudentEverywhere(student)).toBe(true)
    expect(isStudentEverywhere(mentor)).toBe(true)
    expect(isStudentEverywhere(teacherOfA)).toBe(false)
    expect(isStudentEverywhere(admin)).toBe(false)
    expect(isStudentEverywhere(null)).toBe(false)
  })
})
