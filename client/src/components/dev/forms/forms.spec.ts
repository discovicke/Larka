import {flushPromises, mount} from '@vue/test-utils'
import {afterEach, beforeEach, describe, expect, it, vi} from 'vitest'

import CreateContentForm from './CreateContentForm.vue'
import GradeSubmissionForm from './GradeSubmissionForm.vue'
import PublishContentForm from './PublishContentForm.vue'

type Handler = (method: string, path: string, body: unknown) => {status: number, body?: unknown}

const fetchMock = vi.fn<typeof fetch>()

// Svarar på GET för dropdowns och låter testet bestämma svaret på formulärets anrop.
function mockApi(handler: Handler) {
  fetchMock.mockImplementation(async (input, init) => {
    const method = init?.method ?? 'GET'
    const body = init?.body
      ? JSON.parse(init.body as string)
      : undefined
    const response = handler(method, String(input), body)
    return new Response(response.body === undefined
      ? null
      : JSON.stringify(response.body), {
      status: response.status,
      headers: {'Content-Type': 'application/json'},
    })
  })
}

function sent(method: string, path: string) {
  const call = fetchMock.mock.calls.find(([input, init]) => String(input) === path && init?.method === method)
  return call?.[1]?.body
    ? JSON.parse(call[1].body as string)
    : undefined
}

// Dropdowns laddas i flera led (grupper -> kurser -> flöden), så det krävs flera varv.
async function settle() {
  for (let i = 0; i < 10; i++) {
    await flushPromises()
  }
}

const group = {id: 'g1', name: 'NET25', term: 'HT25'}
const assignment = {linkId: 'l1', contentId: 'c1', type: 'Assignment', title: 'Lab 1'}

function backendWithOneGroup(onPost: Handler): Handler {
  return (method, path, body) => {
    if (method !== 'GET') {
      return onPost(method, path, body)
    }
    if (path === '/api/groups') {
      return {status: 200, body: [group]}
    }
    if (path === '/api/groups/g1/content') {
      return {status: 200, body: [assignment]}
    }
    if (path === '/api/links/l1/submissions') {
      return {status: 200, body: [{id: 's1', status: 'Submitted'}]}
    }
    return {status: 200, body: []}
  }
}

describe('dev forms', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    fetchMock.mockReset()
    vi.unstubAllGlobals()
  })

  it('content skickar inte uppgiftsfälten för en nyhet', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'c2', title: 'Hej', type: 'News'}})))
    const wrapper = mount(CreateContentForm)

    await wrapper.find('#content-title').setValue('Hej')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content')).toEqual({
      type: 'News',
      title: 'Hej',
      bodyMd: '',
      status: 'Published',
    })
    expect(wrapper.find('[data-test="result"]').text()).toContain('201')
  })

  it('content skickar uppgiftsfälten för en uppgift med repo', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'c2', title: 'Lab', type: 'Assignment'}})))
    const wrapper = mount(CreateContentForm)

    await wrapper.find('#content-type').setValue('Assignment')
    await wrapper.find('#content-title').setValue('Lab')
    await wrapper.find('#content-max-score').setValue('10')
    await wrapper.find('#content-submission-mode').setValue('Repo')
    await wrapper.find('#content-template-repo').setValue('org/lab')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content')).toMatchObject({
      type: 'Assignment',
      maxScore: 10,
      submissionMode: 'Repo',
      templateRepo: 'org/lab',
    })
  })

  it('publicering kräver grupp eller kurs innan något skickas', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201})))
    const wrapper = mount(PublishContentForm)
    await settle()

    await wrapper.find('#publish-content').setValue('c1')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('[data-test="result"]').text()).toBe('Pick a group, a course or both.')
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('publicering skickar länken och visar felet från backend', async () => {
    mockApi(backendWithOneGroup(() => ({status: 400, body: 'dueAt is only allowed on assignments'})))
    const wrapper = mount(PublishContentForm)
    await settle()

    await wrapper.find('#publish-content').setValue('c1')
    await wrapper.find('#publish-group').setValue('g1')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content/c1/links')).toEqual({
      groupId: 'g1',
      courseId: null,
      publishAt: null,
      dueAt: null,
      isPinned: false,
    })
    expect(wrapper.find('[data-test="result"]').text()).toBe('400: dueAt is only allowed on assignments')
  })

  it('betyg med komplettering skickar ingen poäng', async () => {
    mockApi(backendWithOneGroup(() => ({status: 200, body: {id: 's1', status: 'NeedsWork'}})))
    const wrapper = mount(GradeSubmissionForm)
    await settle()

    await wrapper.find('#grade-submission').setValue('s1')
    await wrapper.find('#grade-needs-work').trigger('click')
    await wrapper.find('#grade-feedback').setValue('Gör om')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade')).toEqual({
      score: null,
      feedback: 'Gör om',
      needsWork: true,
    })
  })
})
