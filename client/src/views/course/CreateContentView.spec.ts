import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import {type Handler, mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {pickDateTime} from '@/test/datePicker'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const week = {
  id: 'm1',
  courseId: 'c1',
  title: 'Vecka 2 – E2E',
  description: '',
  position: 1,
  publishAt: null,
  content: [],
}
const part = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Assignment',
  gradeScale: 'PassFailDistinction',
  title: 'Release-pipeline',
  status: 'Published',
  groupId: null,
  publishAt: '2026-10-20T08:00:00Z',
  dueAt: '2026-10-23T21:59:00Z',
  isPinned: true,
  blockedBy: [],
  hasStudentActivity: false,
}
const content = {
  id: 'x1',
  authorId: 'u1',
  type: 'Assignment',
  title: 'Release-pipeline',
  bodyMd: 'Gör klart pipelinen.',
  status: 'Published',
  createdAt: '2026-10-19T08:00:00Z',
  assignment: {
    gradeScale: 'PassFailDistinction',
    submissionModes: ['File'],
    templateRepo: null,
  },
}

const template = {
  fullName: 'edugrade-net25/mall-lab2',
  htmlUrl: 'https://github.com/edugrade-net25/mall-lab2',
  defaultBranch: 'main',
  isPrivate: true,
  isArchived: false,
}

const templateCommit = {
  sha: 'abc123456789',
  message: 'Template update',
  authoredAt: '2026-10-28T09:30:00Z',
}

function preview(body: Record<string, unknown>) {
  return {
    status: 200,
    body: {name: 'labb-2-elev', pattern: (body.pattern as string | undefined) ?? ''},
  }
}

function backend(onPost: Handler = () => ({status: 201, body: {content, link: {id: 'l1'}}})) {
  mockApi((method, path, body) => {
    if (method !== 'GET') {
      return path === '/api/content/github/repo-name-preview'
        ? preview(body as Record<string, unknown>)
        : onPost(method, path, body)
    }
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'], 'Anna', null, true)}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [week]}
    }
    if (path === '/api/integrations/github/templates') {
      return {status: 200, body: [template]}
    }
    if (path === '/api/integrations/github/templates/edugrade-net25/mall-lab2/latest-commit') {
      return {status: 200, body: templateCommit}
    }
    if (path === '/api/courses/c1/students/unlinked-github') {
      return {
        status: 200,
        body: [{id: 'u-unlinked', name: 'Lina Ahmed', email: 'lina@example.com'}],
      }
    }
    return {status: 200, body: []}
  })
}

function editBackend(
  onWrite: Handler = () => ({status: 200, body: content}),
  attachments: unknown[] = [],
) {
  mockApi((method, path, body) => {
    if (method !== 'GET') {
      return path === '/api/content/github/repo-name-preview'
        ? preview(body as Record<string, unknown>)
        : onWrite(method, path, body)
    }
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'], 'Anna', null, true)}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [{...week, content: [part]}]}
    }
    if (path === '/api/content/x1') {
      return {status: 200, body: content}
    }
    if (path === '/api/content/x1/attachments') {
      return {status: 200, body: attachments}
    }
    if (path === '/api/integrations/github/templates') {
      return {status: 200, body: [template]}
    }
    if (path === '/api/integrations/github/templates/edugrade-net25/mall-lab2/latest-commit') {
      return {status: 200, body: templateCommit}
    }
    if (path === '/api/courses/c1/students/unlinked-github') {
      return {
        status: 200,
        body: [{id: 'u-unlinked', name: 'Lina Ahmed', email: 'lina@example.com'}],
      }
    }
    return {status: 200, body: []}
  })
}

const attachment = {
  id: 'a1',
  contentId: 'x1',
  fileName: 'rapport.pdf',
  url: 'https://files.example/a1',
  sizeBytes: 2048,
}

function dialogButton(label: string): HTMLElement {
  const found = [...document.querySelectorAll('[role="dialog"] button')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!found) {
    throw new Error(`Hittar ingen knapp "${label}" i dialogen`)
  }
  return found as HTMLElement
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

  it('väljer mallrepo från GitHub och visar senaste commit', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#part-Submission').trigger('click')
    await wrapper.find('#mode-Repo').trigger('click')
    await flushPromises()

    const select = wrapper.find('[data-test="template-select"]')
    expect(select.exists()).toBe(true)
    expect(select.findAll('option').map((option) => option.text())).toContain(
      'edugrade-net25/mall-lab2',
    )

    await select.setValue('edugrade-net25/mall-lab2')
    await flushPromises()

    expect(wrapper.find('[data-test="template-commit"]').text()).toContain('Template update')
    expect(wrapper.find('[data-test="template-commit"]').text()).toContain('abc1234')

    await wrapper.find('#title').setValue('Labb 2')
    await wrapper.find('#repo-name-pattern').setValue('lab2-{githubLogin}')
    await flushPromises()
    await flushPromises()

    expect(wrapper.find('[data-test="repo-name-preview"]').text()).toContain('labb-2-elev')
    expect(requests.some((request) =>
      request.method === 'POST' && request.path === '/api/content/github/repo-name-preview')).toBe(true)

    expect(wrapper.find('[data-test="unlinked-students"]').text()).toContain('1 elev har')
    await wrapper.find('[data-test="unlinked-students-toggle"]').trigger('click')
    expect(wrapper.find('[data-test="unlinked-students-list"]').text()).toContain('Lina Ahmed')
    expect(requests.filter((request) =>
      request.method === 'POST' && request.path === '/api/content/with-link')).toHaveLength(0)
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

  it('publicera skapar innehållet och länken i samma anrop och går tillbaka till kursen', async () => {
    backend()
    const {wrapper, router} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#part-Exercise').trigger('click')
    await wrapper.find('#title').setValue('E2E-tester med Playwright')
    button('Publicera').click()
    await flushPromises()

    expect(sent('POST', '/api/content/with-link')).toMatchObject({
      content: {
        type: 'Assignment',
        title: 'E2E-tester med Playwright',
        status: 'Published',
        gradeScale: null,
      },
      link: {courseId: 'c1', moduleId: 'm1'},
    })
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(1)
    expect(router.currentRoute.value.name).toBe('course-content')
  })

  it('publicering och deadline väljs i datumväljarna och skickas som tid', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#part-Exercise').trigger('click')
    await wrapper.find('#title').setValue('Planerad inlämning')
    await pickDateTime('#publish-at', '2027-10-11T09:00')
    await pickDateTime('#due-at', '2027-10-15T17:30')
    expect(wrapper.find('#publish-at').text()).toBe('11 okt 2027 09:00')
    expect(wrapper.find('#due-at').text()).toBe('15 okt 2027 17:30')
    button('Publicera').click()
    await flushPromises()

    expect(sent('POST', '/api/content/with-link')).toMatchObject({
      link: {publishAt: new Date('2027-10-11T09:00').toISOString(), dueAt: new Date('2027-10-15T17:30').toISOString()},
    })
  }, 20000)

  it('spara utkast skickar status Draft', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    await wrapper.find('#title').setValue('Release notes')
    button('Spara utkast').click()
    await flushPromises()

    expect(sent('POST', '/api/content/with-link')).toMatchObject({
      content: {type: 'Theory', status: 'Draft'},
      link: {courseId: 'c1', moduleId: 'm1'},
    })
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

  it('redigerar en del med låst typ och sparar direkt', async () => {
    editBackend()
    const {wrapper, router} = await renderApp('/courses/c1/modules/m1/content/l1/edit')

    expect(wrapper.text()).toContain('Redigera del')
    expect(wrapper.find('[data-test="fixed-part-type"]').text()).toContain('Inlämning')
    expect(wrapper.find('#part-Submission').exists()).toBe(false)
    expect(wrapper.find('#title').element).toHaveProperty('value', 'Release-pipeline')

    await wrapper.find('#title').setValue('Release-pipeline 2')
    button('Spara').click()
    await flushPromises()

    expect(sent('PUT', '/api/content/x1')).toMatchObject({
      title: 'Release-pipeline 2',
      bodyMd: 'Gör klart pipelinen.',
      gradeScale: 'PassFailDistinction',
      submissionModes: ['File'],
    })
    expect(sent('PUT', '/api/content/x1/links/l1')).toMatchObject({
      isPinned: true,
      dueAt: expect.any(String),
    })
    expect(router.currentRoute.value.name).toBe('part')
  })

  it('redigeringsläget listar bilagorna', async () => {
    editBackend(undefined, [attachment])
    const {wrapper} = await renderApp('/courses/c1/modules/m1/content/l1/edit')

    expect(wrapper.find('[data-test="attachments"]').text()).toContain('Bifogade filer')
    expect(wrapper.find('[data-test="attachment-row"]').text()).toContain('rapport.pdf')
    expect(wrapper.find('[data-test="attachment-row"]').text()).toContain('2,0 kB')
    expect(wrapper.find('[data-test="attachment-row"] a').attributes('href')).toBe('https://files.example/a1')
  })

  it('laddar upp en fil som multipart och lägger den sist i listan', async () => {
    const uploaded = {
      id: 'a2',
      contentId: 'x1',
      fileName: 'slides.pdf',
      url: 'https://files.example/a2',
      sizeBytes: 1024,
    }
    editBackend((method, path) => method === 'POST' && path === '/api/content/x1/attachments'
      ? {status: 201, body: uploaded}
      : {status: 404}, [attachment])
    const {wrapper} = await renderApp('/courses/c1/modules/m1/content/l1/edit')

    const append = vi.spyOn(FormData.prototype, 'append')
    const input = wrapper.find('[data-test="attachment-file"]').element as HTMLInputElement
    Object.defineProperty(input, 'files', {
      value: [new File(['pdf'], 'slides.pdf', {type: 'application/pdf'})],
    })
    await wrapper.find('[data-test="attachment-file"]').trigger('change')
    await flushPromises()

    expect(append).toHaveBeenCalledWith('file', expect.any(File), 'slides.pdf')
    const uploadedFile = append.mock.calls.find(([name]) => name === 'file')?.[1] as File
    expect(uploadedFile.type).toBe('application/pdf')
    expect(wrapper.find('[data-test="attachments"]').text()).toContain('slides.pdf')

    append.mockRestore()
  })

  it('frågar först och tar sedan bort filen', async () => {
    editBackend((method) => method === 'DELETE'
      ? {status: 204}
      : {status: 404}, [attachment])
    const {wrapper} = await renderApp('/courses/c1/modules/m1/content/l1/edit')

    await wrapper.find('[aria-label="Ta bort rapport.pdf"]').trigger('click')
    await flushPromises()
    expect(document.body.textContent).toContain('Ta bort filen?')

    dialogButton('Ta bort').click()
    await flushPromises()
    await flushPromises()

    expect(requests
      .filter((request) => request.method === 'DELETE')
      .map((request) => request.path)).toEqual(['/api/content/x1/attachments/a1'])
    expect(wrapper.find('[data-test="attachment-row"]').exists()).toBe(false)
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('i skaparläget visas en påminnelse om att spara delen först', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/modules/m1/new')

    expect(wrapper.find('[data-test="attachments"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Spara delen först, sedan kan du bifoga filer.')
  })

  it('en vecka som inte finns visar sidan som inte finns', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/modules/finns-inte/new')

    expect(wrapper.find('[data-test="not-found"]').exists()).toBe(true)
  })
})
