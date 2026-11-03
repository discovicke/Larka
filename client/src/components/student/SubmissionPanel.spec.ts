import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import type {Schema} from '@/lib/api'
import {mockApi, requests} from '@/test/mockApi'

import SubmissionPanel from './SubmissionPanel.vue'

type Item = Schema<'StudentContentItem'>

const base: Item = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Assignment',
  gradeScale: 'PassFail',
  title: 'Inlämning',
  bodyMd: 'Gör uppgiften',
  publishAt: '2026-01-01T08:00:00Z',
  dueAt: null,
  resubmitDueAt: [],
  isPinned: false,
  isLocked: false,
  isCompleted: false,
  canMarkDone: false,
  blockedBy: [],
  submissionStatus: null,
  gradeLevel: null,
  hasRepoMode: false,
  submissionModes: ['Text'],
  requirements: [],
  repo: null,
}

const file = {id: 'f1', fileName: 'rapport.pdf', url: 'https://files.example/rapport.pdf', sizeBytes: 1500}

function submissionOf(status: Schema<'SubmissionStatus'>, extra: Partial<Schema<'SubmissionDto'>> = {}) {
  return {
    id: 's1',
    contentLinkId: 'l1',
    studentId: 'u1',
    status,
    bodyMd: 'Mitt svar',
    submittedAt: '2026-10-27T10:00:00Z',
    files: [],
    repo: null,
    grade: null,
    ...extra,
  }
}

function repoOf(extra: Partial<Schema<'StudentRepoDto'>> = {}): Schema<'StudentRepoDto'> {
  return {
    repoFullName: 'skol-org/kalle-labb-1',
    lastSha: 'abc1234567890',
    checkStatus: 'Green',
    updatedAt: '2026-11-01T12:00:00Z',
    provisioningState: 'Ready',
    provisioningError: null,
    submissionSha: 'abc1234567890',
    lockedAt: null,
    lockError: null,
    reopenedAt: null,
    lastCommitMessage: 'feat: klar',
    lastCommitAt: '2026-11-01T11:00:00Z',
    commitCount: 3,
    checksPassed: 1,
    checksTotal: 1,
    initialSha: null,
    initialCommitAt: null,
    provisionedAt: null,
    checkStatusChangedAt: null,
    checkRuns: [],
    ...extra,
  }
}

function backend(existing: unknown[] = []) {
  mockApi((method, path) => {
    if (method === 'GET' && path === '/api/links/l1/submissions') {
      return {status: 200, body: existing}
    }
    if (method === 'POST' && path === '/api/links/l1/submissions') {
      return {status: 200, body: submissionOf('Submitted')}
    }
    return {status: 200, body: []}
  })
}

async function show(item: Partial<Item>) {
  const wrapper = mount(SubmissionPanel, {props: {item: {...base, ...item}}, attachTo: document.body})
  await flushPromises()
  return wrapper
}

async function writeAnswer(wrapper: Awaited<ReturnType<typeof show>>, text: string) {
  const markdownTab = wrapper.findAll('button').find((button) => button.text() === 'Markdown')!
  await markdownTab.trigger('click')
  await wrapper.find('textarea').setValue(text)
}

describe('SubmissionPanel', () => {
  it('visar ett textformulär med Lämna in för en uppgift som inte är påbörjad', async () => {
    backend()
    const wrapper = await show({})

    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="submission-file-input"]').exists()).toBe(false)
    const submit = wrapper.find('[data-test="submission-submit"]')
    expect(submit.text()).toBe('Lämna in')
    expect(submit.attributes('disabled')).toBeDefined()
  })

  it('visar formuläret först när inlämningen har hämtats, så att inget skrivet nollställs', async () => {
    let answer: (response: Response) => void = () => {}
    vi.stubGlobal('fetch', () => new Promise<Response>((resolve) => {
      answer = resolve
    }))
    const wrapper = mount(SubmissionPanel, {props: {item: base}, attachTo: document.body})
    await flushPromises()
    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(false)

    answer(new Response('[]', {status: 200, headers: {'Content-Type': 'application/json'}}))
    await flushPromises()
    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
  })

  it('skickar texten som multipart och meddelar att den lämnats in', async () => {
    backend()
    const append = vi.spyOn(FormData.prototype, 'append')
    const wrapper = await show({})

    await writeAnswer(wrapper, 'Det här är mitt svar')
    await wrapper.find('[data-test="submission-form"]').trigger('submit')
    await flushPromises()

    expect(append).toHaveBeenCalledWith('bodyMd', 'Det här är mitt svar')
    expect(requests.some((request) => request.method === 'POST' && request.path === '/api/links/l1/submissions'))
      .toBe(true)
    expect(wrapper.emitted('submitted')).toHaveLength(1)
    append.mockRestore()
  })

  it('låter eleven lämna in senaste commit för en repo-uppgift', async () => {
    backend()
    const wrapper = await show({submissionModes: ['Repo'], hasRepoMode: true})

    expect(wrapper.find('[data-test="repo-submission"]').exists()).toBe(true)
    const submit = wrapper.find('[data-test="repo-submission-submit"]')
    expect(submit.text()).toContain('Lämna in senaste commit')

    await submit.trigger('click')
    await flushPromises()

    expect(requests.some((request) =>
      request.method === 'POST' && request.path === '/api/links/l1/submissions')).toBe(true)
    expect(wrapper.emitted('submitted')).toHaveLength(1)
  })

  it('visar repo-inlämning även när uppgiften också tar text', async () => {
    backend()
    const wrapper = await show({submissionModes: ['Repo', 'Text'], hasRepoMode: true})

    expect(wrapper.find('[data-test="repo-submission"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="repo-submission-submit"]').text()).toContain('Lämna in senaste commit')
    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
  })

  it('visar repo-inlämning även när uppgiften också tar filer', async () => {
    backend()
    const wrapper = await show({submissionModes: ['Repo', 'File'], hasRepoMode: true})

    expect(wrapper.find('[data-test="repo-submission"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="repo-submission-submit"]').text()).toContain('Lämna in senaste commit')
    expect(wrapper.find('[data-test="submission-file-input"]').exists()).toBe(true)
  })

  it('döljer repo-inlämning när repot är låst efter deadline', async () => {
    backend([submissionOf('Submitted', {repo: repoOf({lockedAt: '2026-11-01T12:00:00Z'})})])
    const wrapper = await show({
      submissionModes: ['Repo'],
      hasRepoMode: true,
      submissionStatus: 'Submitted',
      repo: repoOf({lockedAt: '2026-11-01T12:00:00Z'}),
    })

    expect(wrapper.find('[data-test="repo-submission-submit"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="repo-submission"]').text()).toContain('Inlämnad')
  })

  it('visar inlämnad commit även när repot är låst och uppgiften är bedömd', async () => {
    const lockedRepo = repoOf({
      lockedAt: '2026-11-01T12:00:00Z',
      submissionSha: 'abc1234567890',
    })
    backend([submissionOf('Graded', {repo: lockedRepo})])
    const wrapper = await show({
      submissionModes: ['Repo'],
      hasRepoMode: true,
      submissionStatus: 'Graded',
      repo: lockedRepo,
    })

    expect(wrapper.find('[data-test="repo-submission"]').text()).toContain('Inlämnad')
    expect(wrapper.find('[data-test="repo-submission"]').text()).not.toContain('Ingen commit registrerades')
  })

  it('förklarar varför ett låst repo saknar inlämning', async () => {
    backend()
    const wrapper = await show({
      submissionModes: ['Repo'],
      hasRepoMode: true,
      repo: repoOf({
        lockedAt: '2026-11-01T12:00:00Z',
        submissionSha: null,
        lockError: 'Ingen commit före deadline.',
      }),
    })

    expect(wrapper.find('[data-test="repo-submission-locked"]').text()).toContain('Ingen commit registrerades')
    expect(wrapper.find('[data-test="repo-submission-submit"]').exists()).toBe(false)
  })

  it('tar emot filer, listar dem och låter eleven ta bort en före inlämning', async () => {
    backend()
    const append = vi.spyOn(FormData.prototype, 'append')
    const wrapper = await show({submissionModes: ['File']})

    expect(wrapper.find('textarea').exists()).toBe(false)
    const input = wrapper.find('[data-test="submission-file-input"]')
    const files = [new File(['a'], 'a.pdf'), new File(['b'], 'b.png')]
    Object.defineProperty(input.element, 'files', {value: files, configurable: true})
    await input.trigger('change')

    expect(wrapper.findAll('[data-test="submission-file"]')).toHaveLength(2)
    await wrapper.findAll('[data-test="submission-file-remove"]')[0].trigger('click')
    expect(wrapper.findAll('[data-test="submission-file"]')).toHaveLength(1)

    await wrapper.find('[data-test="submission-form"]').trigger('submit')
    await flushPromises()

    expect(append).toHaveBeenCalledWith('files', files[1], 'b.png')
    expect(append).not.toHaveBeenCalledWith('files', files[0], 'a.pdf')
    append.mockRestore()
  })

  it('visar text och fil för en uppgift med båda sätten', async () => {
    backend()
    const wrapper = await show({submissionModes: ['Text', 'File']})

    expect(wrapper.find('[data-test="submission-file-input"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Ditt svar')
  })

  it('visar Bedöms på plats utan formulär', async () => {
    backend()
    const wrapper = await show({submissionModes: ['OnSite']})

    expect(wrapper.find('[data-test="submission-onsite"]').text()).toBe('Bedöms på plats av läraren.')
    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(false)
  })

  it('kombinerar bedömning på plats med ett textformulär', async () => {
    backend()
    const wrapper = await show({submissionModes: ['Text', 'OnSite']})

    expect(wrapper.find('[data-test="submission-onsite"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
  })

  it('visar det som lämnats in och datumet, och låter eleven ändra', async () => {
    backend([submissionOf('Submitted', {files: [file]})])
    const wrapper = await show({submissionModes: ['Text', 'File'], submissionStatus: 'Submitted'})

    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="submission-date"]').text()).toContain('Inlämnad')
    expect(wrapper.find('[data-test="submission-view"]').text()).toContain('rapport.pdf')
    expect(wrapper.find('[data-test="submission-view"] a').attributes('href')).toBe(file.url)

    await wrapper.find('[data-test="submission-edit"]').trigger('click')

    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="submission-submit"]').text()).toBe('Spara ändringarna')
    expect(wrapper.findAll('[data-test="submission-file"]')).toHaveLength(1)
  })

  it('skickar med de filer som ska behållas när eleven ändrar', async () => {
    backend([submissionOf('Submitted', {files: [file]})])
    const append = vi.spyOn(FormData.prototype, 'append')
    const wrapper = await show({submissionModes: ['Text', 'File'], submissionStatus: 'Submitted'})

    await wrapper.find('[data-test="submission-edit"]').trigger('click')
    await wrapper.find('[data-test="submission-form"]').trigger('submit')
    await flushPromises()

    expect(append).toHaveBeenCalledWith('keepFileIds', 'f1')
    append.mockRestore()
  })

  it('visar formuläret med Lämna in igen när läraren vill ha en komplettering', async () => {
    backend([submissionOf('NeedsWork')])
    const wrapper = await show({submissionStatus: 'NeedsWork'})

    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="submission-submit"]').text()).toBe('Lämna in igen')
    expect(wrapper.find('[data-test="submission-submit"]').attributes('disabled')).toBeUndefined()
  })

  it('visar nästa kompletteringsdeadline för den som ska komplettera', async () => {
    backend([submissionOf('NeedsWork')])
    const resubmitDueAt = ['2020-01-01T10:00:00Z', '2099-01-01T10:00:00Z']
    const wrapper = await show({submissionStatus: 'NeedsWork', resubmitDueAt})

    expect(wrapper.find('[data-test="submission-next-resubmit"]').text()).toContain('Nästa kompletteringsdeadline')
    expect(wrapper.find('[data-test="submission-next-resubmit"]').text()).toContain('jan')
  })

  it('döljer nästa kompletteringsdeadline för den som inte ska komplettera', async () => {
    backend([submissionOf('Submitted')])
    const wrapper = await show({submissionStatus: 'Submitted', resubmitDueAt: ['2099-01-01T10:00:00Z']})

    expect(wrapper.find('[data-test="submission-next-resubmit"]').exists()).toBe(false)
  })

  it('visar en bedömd inlämning skrivskyddad', async () => {
    backend([submissionOf('Graded', {files: [file]})])
    const wrapper = await show({submissionModes: ['Text', 'File'], submissionStatus: 'Graded', gradeLevel: 'Pass'})

    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="submission-edit"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="submission-view"]').text()).toContain('rapport.pdf')
  })

  it('varnar för sen inlämning men låter eleven lämna in', async () => {
    backend()
    const wrapper = await show({dueAt: '2026-01-02T08:00:00Z'})

    expect(wrapper.find('[data-test="submission-late"]').text()).toContain('Sen inlämning')
    expect(wrapper.find('[data-test="submission-form"]').exists()).toBe(true)
  })

  it('varnar inte för sen inlämning efter betyg eller före deadline', async () => {
    backend([submissionOf('Graded')])
    const graded = await show({dueAt: '2026-01-02T08:00:00Z', submissionStatus: 'Graded'})
    expect(graded.find('[data-test="submission-late"]').exists()).toBe(false)

    backend()
    const early = await show({dueAt: '2999-01-01T08:00:00Z'})
    expect(early.find('[data-test="submission-late"]').exists()).toBe(false)
  })

  it('visar felet från servern och lämnar formuläret kvar', async () => {
    mockApi((method) => method === 'POST'
      ? {status: 400, body: {error: 'already graded'}}
      : {status: 200, body: []})
    const wrapper = await show({})

    await writeAnswer(wrapper, 'Svar')
    await wrapper.find('[data-test="submission-form"]').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
    expect(wrapper.emitted('submitted')).toBeUndefined()
  })
})
