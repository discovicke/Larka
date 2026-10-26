import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {type Handler, mockApi, requests, sent} from '@/test/mockApi'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const week = {id: 'm1', courseId: 'c1', title: 'Vecka 2 – E2E', description: '', position: 1, content: []}

function backend(onPost: Handler = () => ({status: 201, body: {id: 'x1'}})) {
  mockApi((method, path, body) => {
    if (method !== 'GET') {
      return onPost(method, path, body)
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [week]}
    }
    return {status: 200, body: []}
  })
}

function button(label: string): HTMLButtonElement {
  const found = [...document.querySelectorAll<HTMLButtonElement>('#page-actions button')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!found) {
    throw new Error(`Hittar ingen knapp "${label}" i headern`)
  }
  return found
}

describe('CreateContentView', () => {
  it('visar veckan, börjar på teori och visar fälten för typen man väljer', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')
    expect(wrapper.text()).toContain('Vecka 2 – E2E')
    expect(wrapper.find('#part-Theory').attributes('data-state')).toBe('checked')

    await wrapper.find('#part-Submission').trigger('click')

    expect(wrapper.findAll('#grade-scale option').map((option) => option.text())).toEqual(['IG – G', 'IG – G – VG'])
    expect(wrapper.text()).toContain('Hur lämnar eleven in?')
  })

  it('teori har varken deadline, betygsskala eller inlämning', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    expect(wrapper.find('#due-at').exists()).toBe(false)
    expect(wrapper.find('#grade-scale').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Hur lämnar eleven in?')
  })

  it('sparar inget utan titel', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    button('Publicera').click()
    await flushPromises()

    expect(wrapper.text()).toContain('Ange en titel.')
    expect(requests.some((request) => request.method === 'POST')).toBe(false)
  })

  it('publicera skapar innehållet, lägger det i veckan och går tillbaka till kursen', async () => {
    backend()
    const {wrapper, router} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#part-Exercise').trigger('click')
    await wrapper.find('#title').setValue('E2E-tester med Playwright')
    button('Publicera').click()
    await flushPromises()

    expect(sent('POST', '/api/content')).toMatchObject({
      type: 'Assignment',
      title: 'E2E-tester med Playwright',
      status: 'Published',
      gradeScale: null,
    })
    expect(sent('POST', '/api/content/x1/links')).toMatchObject({courseId: 'c1', moduleId: 'm1'})
    expect(router.currentRoute.value.name).toBe('course-content')
  })

  it('spara utkast skickar status Draft', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#title').setValue('Release notes')
    button('Spara utkast').click()
    await flushPromises()

    expect(sent('POST', '/api/content')).toMatchObject({type: 'Theory', status: 'Draft'})
  })

  it('visar felet från servern och stannar på sidan', async () => {
    backend(() => ({status: 400, body: 'templateRepo must look like org/name'}))
    const {wrapper, router} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#title').setValue('Teori')
    button('Publicera').click()
    await flushPromises()

    expect(wrapper.text()).toContain('templateRepo must look like org/name')
    expect(router.currentRoute.value.name).toBe('create-content')
  })

  it('en vecka som inte finns visar sidan som inte finns', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/modules/finns-inte/new')

    expect(wrapper.find('[data-test="not-found"]').exists()).toBe(true)
  })
})
