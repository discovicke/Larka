import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import type {StudyPlan, StudyPlanAssignment, StudyPlanCourse} from '@/lib/studyPlan'
import {mockApi, respond} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const devops: Schema<'CourseDto'> = {
  id: 'c1',
  schoolId: 's1',
  code: 'DEVOPS',
  title: 'DevOps',
  description: '',
  points: 25,
  startsOn: '2026-10-01',
  endsOn: '2026-11-13',
}

function planOf(courses: StudyPlanCourse[], completedPoints = 0, totalPoints = 0): StudyPlan {
  return {completedPoints, totalPoints, courses}
}

function course(options: Partial<StudyPlanCourse> = {}): StudyPlanCourse {
  return {
    courseId: 'c1',
    title: 'DevOps',
    code: 'DEVOPS',
    points: 25,
    startsOn: null,
    endsOn: null,
    status: 'Ongoing',
    grade: null,
    assignmentCount: 0,
    submittedCount: 0,
    gradedCount: 0,
    assignments: [],
    ...options,
  }
}

function assignment(options: Partial<StudyPlanAssignment> = {}): StudyPlanAssignment {
  return {
    linkId: 'l1',
    title: 'Laboration',
    dueAt: null,
    status: 'NotStarted',
    grade: null,
    feedback: null,
    gradedAt: null,
    ...options,
  }
}

interface BackendOptions {
  plan?: StudyPlan
  me?: ReturnType<typeof meAs>
  fail?: boolean
}

function backend(options: BackendOptions = {}) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return respond<'/api/me', 'get'>(options.me ?? meAs('Student', ['c1']))
    }
    if (path === '/api/courses') {
      return respond<'/api/courses', 'get'>([devops])
    }
    if (path === '/api/me/study-plan') {
      return options.fail
        ? {status: 500, body: 'Server error'}
        : respond<'/api/me/study-plan', 'get'>(options.plan ?? planOf([]))
    }
    return {status: 404}
  })
}

describe('GradesView', () => {
  it('visar kurser med period, status, betyg och poängsumma', async () => {
    backend({
      plan: planOf([
        course({
          courseId: 'c1',
          title: 'Objektorienterad programmering',
          status: 'Completed',
          grade: 'Distinction',
          points: 60,
          startsOn: '2025-09-01',
          endsOn: '2025-11-30',
        }),
        course({
          courseId: 'c2',
          title: 'DevOps',
          status: 'Ongoing',
          points: 25,
          startsOn: '2026-10-01',
          endsOn: '2026-11-13',
        }),
      ], 60, 85),
    })

    const {wrapper, router} = await renderApp('/grades')

    expect(router.currentRoute.value.name).toBe('grades')
    const rows = wrapper.findAll('[data-test="study-plan-course"]')
    expect(rows).toHaveLength(2)
    expect(rows[0].findAll('td').map((cell) => cell.text()))
      .toEqual(['Objektorienterad programmering', 'sep – nov 2025', '60', 'Klar', 'VG'])
    expect(rows[1].text()).toContain('Pågår')
    expect(rows[1].text()).toContain('Ej satt')
    // Samma kurser som staplade kort för små skärmar: titel, period · poäng, status och betyg.
    const cards = wrapper.findAll('[data-test="study-plan-course-card"]')
    expect(cards).toHaveLength(2)
    expect(cards[0].text()).toContain('Objektorienterad programmering')
    expect(cards[0].text()).toContain('sep – nov 2025 · 60 p')
    expect(cards[0].text()).toContain('Klar')
    expect(cards[0].text()).toContain('VG')
    expect(cards[1].text()).toContain('Ej satt')
    expect(wrapper.text()).toContain('60 p')
    expect(wrapper.text()).toContain('av 85 p')
    expect(wrapper.text()).toContain('Pågår nu')
    expect(wrapper.text()).toContain('DevOps · 25 p')
    expect(wrapper.text()).toContain('Slutar 13 nov')
  })

  it('visar betyg på kursdelar och fäller ut återkopplingen', async () => {
    backend({
      plan: planOf([
        course({
          courseId: 'c1',
          title: 'DevOps',
          status: 'Ongoing',
          startsOn: '2026-10-01',
          endsOn: '2026-11-13',
          assignmentCount: 2,
          submittedCount: 1,
          gradedCount: 1,
          assignments: [
            assignment({
              linkId: 'l1',
              title: 'Laboration: CI/CD',
              status: 'Graded',
              grade: 'Pass',
              feedback: 'Bra jobbat',
            }),
            assignment({
              linkId: 'l2',
              title: 'Inlämningsuppgift 1',
              dueAt: '2099-01-01T16:00:00Z',
            }),
          ],
        }),
      ], 0, 25),
    })

    const {wrapper} = await renderApp('/grades')

    expect(wrapper.text()).toContain('Betyg på kursdelar')
    const rows = wrapper.findAll('[data-test="study-plan-assignment"]')
    expect(rows).toHaveLength(2)
    expect(rows[0].text()).toContain('Laboration: CI/CD')
    expect(rows[0].text()).toContain('G')
    expect(rows[1].text()).toContain('Pågår · slutar 1 jan')
    expect(wrapper.find('[data-test="study-plan-feedback"]').exists()).toBe(false)

    await rows[0].find('button').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="study-plan-feedback"]').text()).toBe('Bra jobbat')
  })

  it('visar tomvyn när eleven inte har några kurser', async () => {
    backend()

    const {wrapper} = await renderApp('/grades')

    expect(wrapper.text()).toContain('Ingen studieplan än')
    expect(wrapper.text()).toContain('Kurserna du läser som elev visas här med poäng och betyg.')
    expect(wrapper.findAll('[data-test="study-plan-course"]')).toHaveLength(0)
  })

  it('visar ett fel när studieplanen inte kan hämtas', async () => {
    backend({fail: true})

    const {wrapper} = await renderApp('/grades')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta studieplanen.')
    expect(wrapper.findAll('[data-test="study-plan-course"]')).toHaveLength(0)
  })

  it('visar Studieplan i sidomenyn för en elev', async () => {
    backend({me: meAs('Student', ['c1'])})

    const {wrapper, router} = await renderApp('/')

    const link = wrapper.find('[data-slot="sidebar"] a[href="/grades"]')
    expect(link.exists()).toBe(true)
    expect(link.text()).toContain('Studieplan')

    await link.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('grades')
  })

  it('döljer Studieplan i sidomenyn för en lärare', async () => {
    backend({me: meAs('Teacher', ['c1'])})

    const {wrapper} = await renderApp('/')

    expect(wrapper.find('[data-slot="sidebar"] a[href="/grades"]').exists()).toBe(false)
  })
})
