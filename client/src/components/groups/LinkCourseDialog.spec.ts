import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it, vi} from 'vitest'

import LinkCourseDialog from '@/components/groups/LinkCourseDialog.vue'
import {mockApi, requests} from '@/test/mockApi'

const courses = [
  {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'},
  {id: 'c2', schoolId: 's1', code: 'JAVA', title: 'Java'},
]

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

async function openDialog(status = 201, list: unknown[] = courses) {
  mockApi((method) => (method === 'GET'
    ? {status: 200, body: list}
    : {status, body: status === 201
        ? {}
        : 'fel'}))
  const wrapper = mount(LinkCourseDialog, {
    props: {open: false, groupId: 'g1', groupName: 'TE22A', linkedIds: ['c1']},
    attachTo: document.body,
  })
  await wrapper.setProps({open: true})
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

// Kurserna i listan under sökfältet, i ordning.
const shown = () => [...document.querySelectorAll<HTMLInputElement>('[data-test="link-course-radio"]')].map((radio) => radio.value)

async function pick(value: string) {
  document.querySelector<HTMLInputElement>(`[data-test="link-course-radio"][value="${value}"]`)!.click()
  await flushPromises()
  ;(document.querySelector('[data-test="confirm-link-course"]') as HTMLElement).click()
  await flushPromises()
}

describe('LinkCourseDialog', () => {
  it('listar bara kurser som inte redan är kopplade', async () => {
    await openDialog()

    expect(shown()).toEqual(['c2'])
    expect(document.querySelector('[data-test="link-course-count"]')?.textContent?.trim()).toBe('1 kurs')
  })

  it('lägger avslutade kurser sist', async () => {
    await openDialog(201, [
      courses[0],
      {id: 'c3', schoolId: 's1', code: 'OLD', title: 'Gammal', startsOn: '2020-01-01', endsOn: '2020-06-01'},
      courses[1],
    ])

    expect(shown()).toEqual(['c2', 'c3'])
  })

  it('filtrerar kurserna på titel och kod i sökfältet', async () => {
    mockApi(() => ({status: 200, body: [...courses, {id: 'c3', schoolId: 's1', code: 'NET', title: 'C# grunder'}]}))
    const wrapper = mount(LinkCourseDialog, {
      props: {open: false, groupId: 'g1', groupName: 'TE22A', linkedIds: ['c1']},
      attachTo: document.body,
    })
    await wrapper.setProps({open: true})
    await flushPromises()
    mounted.push(wrapper)
    const search = document.querySelector('[data-test="link-course-search"]') as HTMLInputElement

    search.value = 'grund'
    search.dispatchEvent(new Event('input'))
    await flushPromises()
    expect(shown()).toEqual(['c3'])
    expect(document.querySelector('[data-test="link-course-count"]')?.textContent?.trim()).toBe('1 av 2 kurser')

    search.value = 'java'
    search.dispatchEvent(new Event('input'))
    await flushPromises()
    expect(shown()).toEqual(['c2'])

    search.value = 'finns inte'
    search.dispatchEvent(new Event('input'))
    await flushPromises()
    expect(shown()).toEqual([])
    expect(document.querySelector('[data-test="link-course-no-match"]')).not.toBeNull()
  })

  it('kopplar vald kurs och meddelar det', async () => {
    const wrapper = await openDialog()

    await pick('c2')

    expect(requests.at(-1)).toMatchObject({method: 'POST', path: '/api/groups/g1/courses/c2'})
    await vi.waitFor(() => expect(wrapper.emitted('linked')).toBeDefined())
    expect(wrapper.emitted('linked')?.[0]).toEqual([courses[1]])
  })

  it('säger att kursen redan är kopplad vid 409', async () => {
    const wrapper = await openDialog(409)

    await pick('c2')

    expect(document.querySelector('[role="alert"]')?.textContent).toContain('Kursen är redan kopplad till gruppen.')
    expect(wrapper.emitted('linked')).toBeUndefined()
  })

  it('visar andra fel', async () => {
    await openDialog(500)

    await pick('c2')

    expect(document.querySelector('[role="alert"]')?.textContent).toContain('fel')
  })
})
