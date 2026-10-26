import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {type Handler, mockApi, requests, sent} from '@/test/mockApi'

import AddGroupMemberForm from './AddGroupMemberForm.vue'
import CreateAttachmentForm from './CreateAttachmentForm.vue'
import CreateContentForm from './CreateContentForm.vue'
import CreateGroupForm from './CreateGroupForm.vue'
import CreateModuleForm from './CreateModuleForm.vue'
import CreateSubmissionForm from './CreateSubmissionForm.vue'
import CreateUserForm from './CreateUserForm.vue'
import GradeSubmissionForm from './GradeSubmissionForm.vue'
import PublishContentForm from './PublishContentForm.vue'

function posted() {
  return requests.some((request) => request.method === 'POST')
}

// Dropdowns laddas i flera led (grupper -> kurser -> flöden), så det krävs flera varv.
async function settle() {
  for (let i = 0; i < 10; i++) {
    await flushPromises()
  }
}

const school = {id: 'sc1', name: 'Edugrade'}
const user = {id: 'u1', name: 'Maria Ek', role: 'Teacher'}
const group = {id: 'g1', name: 'NET25', term: 'HT25'}
const course = {id: 'co1', code: 'DEVOPS', title: 'DevOps'}
const week = {id: 'm1', title: 'Vecka 1', content: []}
const assignment = {linkId: 'l1', contentId: 'c1', type: 'Assignment', title: 'Lab 1'}

function backendWithOneGroup(onPost: Handler): Handler {
  return (method, path, body) => {
    if (method !== 'GET') {
      return onPost(method, path, body)
    }
    if (path === '/api/schools') {
      return {status: 200, body: [school]}
    }
    if (path === '/api/users') {
      return {status: 200, body: [user]}
    }
    if (path === '/api/groups') {
      return {status: 200, body: [group]}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/co1/modules') {
      return {status: 200, body: [week]}
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

  it('content skickar uppgiftsfälten för en inlämning med text och repo', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'c2', title: 'Lab', type: 'Assignment'}})))
    const wrapper = mount(CreateContentForm)

    await wrapper.find('#content-type').setValue('Assignment')
    await wrapper.find('#content-title').setValue('Lab')
    await wrapper.find('#content-grade-scale').setValue('PassFailDistinction')
    await wrapper.find('#content-mode-Text').trigger('click')
    await wrapper.find('#content-mode-Repo').trigger('click')
    await wrapper.find('#content-template-repo').setValue('org/lab')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content')).toMatchObject({
      type: 'Assignment',
      gradeScale: 'PassFailDistinction',
      submissionModes: ['Text', 'Repo'],
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
    expect(posted()).toBe(false)
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
      moduleId: null,
      publishAt: null,
      dueAt: null,
      isPinned: false,
    })
    expect(wrapper.find('[data-test="result"]').text()).toBe('400: dueAt is only allowed on assignments')
  })

  it('publicering i en vecka skickar kursen och veckan', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'l2'}})))
    const wrapper = mount(PublishContentForm)
    await settle()

    await wrapper.find('#publish-content').setValue('c1')
    await wrapper.find('#publish-course').setValue('co1')
    await wrapper.find('#publish-module').setValue('m1')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content/c1/links')).toMatchObject({courseId: 'co1', moduleId: 'm1'})
  })

  it('vecka skickas till vald kurs, utan tom beskrivning', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'm2', title: 'Vecka 2'}})))
    const wrapper = mount(CreateModuleForm)
    await settle()

    await wrapper.find('#module-course').setValue('co1')
    await wrapper.find('#module-title').setValue('Vecka 2')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/courses/co1/modules')).toEqual({title: 'Vecka 2', description: null})
    expect(wrapper.find('[data-test="result"]').text()).toContain('201')
  })

  it('vecka kräver en kurs innan något skickas', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201})))
    const wrapper = mount(CreateModuleForm)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('[data-test="result"]').text()).toBe('Pick a course.')
    expect(posted()).toBe(false)
  })

  it('betyg med komplettering skickar inget betyg', async () => {
    mockApi(backendWithOneGroup(() => ({status: 200, body: {id: 's1', status: 'NeedsWork'}})))
    const wrapper = mount(GradeSubmissionForm)
    await settle()

    await wrapper.find('#grade-submission').setValue('s1')
    await wrapper.find('#grade-needs-work').trigger('click')
    await wrapper.find('#grade-feedback').setValue('Gör om')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade')).toEqual({
      level: null,
      feedback: 'Gör om',
      needsWork: true,
    })
  })

  it('betyg skickar vald nivå', async () => {
    mockApi(backendWithOneGroup(() => ({status: 200, body: {id: 's1', status: 'Graded'}})))
    const wrapper = mount(GradeSubmissionForm)
    await settle()

    await wrapper.find('#grade-submission').setValue('s1')
    await wrapper.find('#grade-level').setValue('Distinction')
    await wrapper.find('#grade-feedback').setValue('Bra')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade')).toEqual({
      level: 'Distinction',
      feedback: 'Bra',
      needsWork: false,
    })
  })

  it('användare skickas med vald skola och roll', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'u2', name: 'Ada', role: 'Student'}})))
    const wrapper = mount(CreateUserForm)
    await settle()

    await wrapper.find('#user-school').setValue('sc1')
    await wrapper.find('#user-name').setValue('Ada')
    await wrapper.find('#user-email').setValue('ada@example.com')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/users')).toEqual({
      schoolId: 'sc1',
      name: 'Ada',
      email: 'ada@example.com',
      role: 'Student',
    })
    expect(wrapper.find('[data-test="result"]').text()).toContain('201')
  })

  it('grupp utan termin skickar term som null och visar felet från backend', async () => {
    mockApi(backendWithOneGroup(() => ({status: 400, body: 'school does not exist'})))
    const wrapper = mount(CreateGroupForm)
    await settle()

    await wrapper.find('#group-school').setValue('sc1')
    await wrapper.find('#group-name').setValue('Elevråd')
    await wrapper.find('#group-type').setValue('Committee')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/groups')).toEqual({schoolId: 'sc1', type: 'Committee', name: 'Elevråd', term: null})
    expect(wrapper.find('[data-test="result"]').text()).toBe('400: school does not exist')
  })

  it('medlem skickas till vald grupp', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {userId: 'u1', groupId: 'g1', role: 'Teacher'}})))
    const wrapper = mount(AddGroupMemberForm)
    await settle()

    await wrapper.find('#member-group').setValue('g1')
    await wrapper.find('#member-user').setValue('u1')
    await wrapper.find('#member-role').setValue('Teacher')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/groups/g1/members')).toEqual({userId: 'u1', role: 'Teacher'})
  })

  it('medlem kräver grupp och användare innan något skickas', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201})))
    const wrapper = mount(AddGroupMemberForm)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('[data-test="result"]').text()).toBe('Pick a group and a user.')
    expect(posted()).toBe(false)
  })

  it('bilaga skickas som multipart till vald content', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201, body: {id: 'a1', fileName: 'slides.pdf'}})))
    const wrapper = mount(CreateAttachmentForm)
    await settle()

    await wrapper.find('#attachment-content').setValue('c1')
    const input = wrapper.find('#attachment-file').element as HTMLInputElement
    Object.defineProperty(input, 'files', {value: [new File(['pdf'], 'slides.pdf', {type: 'application/pdf'})]})
    await wrapper.find('#attachment-file').trigger('change')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const upload = sent('POST', '/api/content/c1/attachments') as string
    expect(upload).toContain('name="file"')
    expect(upload).toContain('Content-Type: application/pdf')
    expect(wrapper.find('[data-test="result"]').text()).toContain('201')
  })

  it('bilaga kräver innehåll och fil innan något skickas', async () => {
    mockApi(backendWithOneGroup(() => ({status: 201})))
    const wrapper = mount(CreateAttachmentForm)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('[data-test="result"]').text()).toBe('Pick the content to attach the file to.')
    expect(posted()).toBe(false)
  })

  it('inlämning skickas med text och filer som multipart', async () => {
    mockApi(backendWithOneGroup(() => ({status: 200, body: {id: 's2', status: 'Submitted'}})))
    const wrapper = mount(CreateSubmissionForm)
    await settle()

    await wrapper.find('#submission-link').setValue('l1')
    await wrapper.find('#submission-body').setValue('Se bilagan')
    const input = wrapper.find('#submission-files').element as HTMLInputElement
    Object.defineProperty(input, 'files', {value: [new File(['pdf'], 'rapport.pdf', {type: 'application/pdf'})]})
    await wrapper.find('#submission-files').trigger('change')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    const submitted = sent('POST', '/api/links/l1/submissions') as string
    expect(submitted).toContain('name="bodyMd"')
    expect(submitted).toContain('Se bilagan')
    expect(submitted).toContain('name="files"')
    expect(submitted).toContain('Content-Type: application/pdf')
  })
})
