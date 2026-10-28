import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {pickDate} from '@/test/datePicker'
import {renderApp} from '@/test/renderApp'

const course = {
  id: 'c1',
  schoolId: 's1',
  code: 'DEVOPS',
  title: 'DevOps',
  description: 'Kursen handlar om **CI/CD** och drift.',
  points: 25,
  startsOn: '2026-10-12',
  endsOn: '2026-11-13',
}

const anna = {
  userId: 'u1',
  name: 'Anna Andersson',
  email: 'anna@example.com',
  groups: ['NET25', 'Lag 1'],
}
const bo = {
  userId: 'u2',
  name: 'Bo Berg',
  email: 'bo@example.com',
  groups: ['Lag 1'],
}

interface Backend {
  course?: typeof course
  staff?: unknown[]
  failStaff?: boolean
  me?: ReturnType<typeof meAs>
}

function backend(options: Backend = {}) {
  const currentCourse = options.course ?? course
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: options.me ?? meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [currentCourse]}
    }
    if (path === '/api/courses/c1/staff') {
      return options.failStaff
        ? {status: 500, body: 'Server error'}
        : {status: 200, body: options.staff ?? [anna, bo]}
    }
    if (path === '/api/courses/c1' && method === 'PUT') {
      return {status: 200, body: {...currentCourse, ...(body as object)}}
    }
    return {status: 404}
  })
}

function dialogButton(label: string): HTMLElement {
  const button = [...document.querySelectorAll('[role="dialog"] button')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!button) {
    throw new Error(`No dialog button: ${label}`)
  }
  return button as HTMLElement
}

async function fill(selector: string, value: string) {
  const field = document.querySelector(selector) as HTMLInputElement | HTMLTextAreaElement | null
  if (!field) {
    throw new Error(`Hittar inte fältet ${selector}`)
  }
  field.value = value
  field.dispatchEvent(new Event('input'))
  await flushPromises()
}

async function submitDialogForm() {
  const form = document.querySelector('[role="dialog"] form') as HTMLFormElement
  form.dispatchEvent(new Event('submit'))
  await flushPromises()
  await flushPromises()
}

describe('CourseAboutView', () => {
  it('visar beskrivningen, personalen och fakta', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/about')

    expect(wrapper.find('h1').text()).toBe('DevOps')
    expect(wrapper.text()).toContain('KURS · DEVOPS')
    expect(wrapper.text()).toContain('12 okt – 13 nov 2026 · 25 p')
    expect(wrapper.find('article strong, section strong').text()).toBe('CI/CD')

    const rows = wrapper.findAll('[data-test="staff-row"]')
    expect(rows.map((row) => row.text())).toEqual([
      expect.stringContaining('Anna Andersson'),
      expect.stringContaining('Bo Berg'),
    ])
    expect(rows[0].text()).toContain('NET25 · Lag 1')
    expect(rows[0].find('a').attributes('href')).toBe('mailto:anna@example.com')
    expect(rows[0].find('a').text()).toContain('Meddelande')

    expect(wrapper.text()).toContain('2026-10-12 – 2026-11-13')
    expect(wrapper.text()).toContain('IG – G – VG')
  })

  it('personalfilen visar samma sida med personalen', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/staff')

    expect(wrapper.find('#personal').exists()).toBe(true)
    expect(wrapper.find('#staff-heading').text()).toBe('Personal')
    expect(wrapper.findAll('[data-test="staff-row"]')).toHaveLength(2)
  })

  it('läraren kan redigera kursen och ändringen sparas', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/about')

    await wrapper.find('[data-test="edit-course"]').trigger('click')
    await flushPromises()
    await fill('#course-edit-title', 'DevOps 2')
    dialogButton('Markdown').click()
    await flushPromises()
    await fill('#course-edit-description', 'Ny **beskrivning**')
    await fill('#course-edit-points', '30')
    await pickDate('#course-edit-start', '2026-10-01')
    await pickDate('#course-edit-end', '2026-12-01')
    await submitDialogForm()

    expect(sent('PUT', '/api/courses/c1')).toEqual({
      title: 'DevOps 2',
      description: 'Ny **beskrivning**',
      points: 30,
      startsOn: '2026-10-01',
      endsOn: '2026-12-01',
    })
    expect(document.querySelector('[role="dialog"]')).toBeNull()
    expect(wrapper.find('h1').text()).toBe('DevOps 2')
    expect(wrapper.text()).toContain('Ny')
  })

  it('visar felmeddelandet från API:et när sparningen misslyckas', async () => {
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/staff') {
        return {status: 200, body: [anna]}
      }
      if (method === 'PUT') {
        return {status: 400, body: 'points must be between 1 and 400'}
      }
      return {status: 404}
    })

    const {wrapper} = await renderApp('/courses/c1/about')

    await wrapper.find('[data-test="edit-course"]').trigger('click')
    await flushPromises()
    await fill('#course-edit-points', '500')
    await submitDialogForm()

    expect(document.querySelector('[role="dialog"]')?.textContent)
      .toContain('points must be between 1 and 400')
  })

  it('en elev ser ingen Redigera-knapp', async () => {
    backend({me: meAs('Student', ['c1'], 'Bo Elev')})

    const {wrapper} = await renderApp('/courses/c1/about')

    expect(wrapper.find('[data-test="edit-course"]').exists()).toBe(false)
    expect(wrapper.findAll('[data-test="staff-row"]')).toHaveLength(2)
  })

  it('visar en ledtråd när kursen saknar beskrivning', async () => {
    backend({course: {...course, description: ''}})

    const {wrapper} = await renderApp('/courses/c1/about')

    expect(wrapper.text()).toContain('Ingen kursbeskrivning ännu.')
  })

  it('visar ett fel när personalen inte kan hämtas', async () => {
    backend({failStaff: true})

    const {wrapper} = await renderApp('/courses/c1/about')

    expect(wrapper.find('[role="alert"]').text()).toContain('Kunde inte hämta personalen.')
    expect(wrapper.findAll('[data-test="staff-row"]')).toHaveLength(0)
  })
})
