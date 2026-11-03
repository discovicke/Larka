import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {mentorAs, meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

const repo = {
  repoFullName: 'anna/release-pipeline',
  lastSha: '0123456789abcdef',
  lastCommitMessage: 'feat: release pipeline',
  lastCommitAt: '2026-10-23T20:00:00Z',
  commitCount: 14,
  checkStatus: 'Green',
  checksPassed: 12,
  checksTotal: 12,
  updatedAt: '2026-10-23T20:00:00Z',
  provisioningState: 'Ready',
  provisioningError: null,
  submissionSha: null,
  lockedAt: null,
  lockError: null,
  reopenedAt: '2026-10-30T10:00:00Z',
  checkRuns: [],
}

const baseSubmission = {
  id: 's1',
  contentLinkId: 'l1',
  studentId: 'u1',
  studentName: 'Anna Andersson',
  status: 'Submitted',
  bodyMd: 'Min **lösning** finns i repot.',
  submittedAt: '2026-10-21T10:00:00Z',
  files: [{id: 'f1', fileName: 'rapport.pdf', url: 'https://files.example/f1', sizeBytes: 1200}],
  repo,
  grade: null,
}

const gradedSubmission = {
  ...baseSubmission,
  status: 'Graded',
  grade: {
    gradedById: 'u0',
    level: 'Pass',
    feedback: 'Tidigare kommentar',
    gradedAt: '2026-10-24T09:00:00Z',
  },
}

function week(scale: string, dueAt: string | null = null, resubmitDueAt: string[] = []) {
  return {
    id: 'm1',
    courseId: 'c1',
    title: 'Vecka 4',
    description: '',
    position: 4,
    content: [{
      linkId: 'l1',
      contentId: 'x1',
      type: 'Assignment',
      gradeScale: scale,
      completionRule: null,
      title: 'Release-pipeline',
      status: 'Published',
      groupId: null,
      publishAt: '2026-10-01T08:00:00Z',
      dueAt,
      resubmitDueAt,
      isPinned: false,
      blockedBy: [],
    }],
  }
}

const content = {
  id: 'x1',
  authorId: 'u0',
  type: 'Assignment',
  title: 'Release-pipeline',
  bodyMd: 'Lämna in **länken** till repot.',
  status: 'Published',
  createdAt: '2026-10-01T08:00:00Z',
  assignment: {gradeScale: 'PassFail', submissionModes: ['Text', 'Repo'], templateRepo: null},
}

function queued(id: string, studentName: string, status = 'Submitted') {
  return {...baseSubmission, id, studentName, status, repo: null, files: []}
}

function backend(options: {
  scale?: string
  submission?: unknown
  me?: ReturnType<typeof meAs>
  queue?: unknown[]
  otherAssignment?: unknown[]
  modes?: string[]
  dueAt?: string
  resubmitDueAt?: string[]
} = {}) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: options.me ?? meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      const first = week(options.scale ?? 'PassFail', options.dueAt ?? null, options.resubmitDueAt ?? [])
      return {
        status: 200,
        body: [options.otherAssignment
          ? {...first, content: [...first.content, {...first.content[0], linkId: 'l2', contentId: 'x2', title: 'Docker'}]}
          : first],
      }
    }
    if (path === '/api/content/x1') {
      return {
        status: 200,
        body: options.modes
          ? {...content, assignment: {...content.assignment, submissionModes: options.modes}}
          : content,
      }
    }
    if (path === '/api/submissions/s1' && method === 'GET') {
      return {status: 200, body: options.submission ?? baseSubmission}
    }
    const other = /^\/api\/submissions\/(s\d+)$/.exec(path)
    if (other && method === 'GET') {
      const found = (options.queue ?? []).find((candidate) => (candidate as {id: string}).id === other[1])
      return found
        ? {status: 200, body: found}
        : {status: 404}
    }
    if (/^\/api\/submissions\/s\d+\/grade$/.test(path) && method === 'POST') {
      return {status: 200, body: baseSubmission}
    }
    if (path === '/api/submissions/s1/grade' && method === 'POST') {
      return {status: 200, body: baseSubmission}
    }
    if (path === '/api/links/l2/submissions') {
      return {status: 200, body: (options.otherAssignment ?? []).map((entry) => ({...(entry as object), contentLinkId: 'l2'}))}
    }
    if (path === '/api/submissions/s6' || path === '/api/submissions/s7') {
      const found = (options.otherAssignment ?? []).find((entry) => `/api/submissions/${(entry as {id: string}).id}` === path)
      return {status: 200, body: {...(found as object), contentLinkId: 'l2'}}
    }
    if (path === '/api/links/l1/submissions') {
      return {status: 200, body: options.queue ?? []}
    }
    return {status: 404}
  })
}

function text(element: {text: () => string}): string {
  return element
    .text()
    .replace(/\s+/g, ' ')
    .trim()
}

function button(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], label: string) {
  const found = wrapper.findAll('button')
    .find((candidate) => text(candidate) === label)
  if (!found) {
    throw new Error(`No button: ${label}`)
  }
  return found
}

async function radiosButton(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], label: string) {
  const found = wrapper.findAll('[role="radio"]').find((candidate) => text(candidate).startsWith(label))
  await found!.trigger('click')
}

function radios(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[role="radio"]').map(text)
}

describe('ReviewView', () => {
  it('mentorn läser inlämningen och betyget men kan inte bedöma', async () => {
    backend({me: mentorAs(['c1']), submission: gradedSubmission})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('h1').text()).toBe('Anna Andersson')
    expect(wrapper.find('strong').text()).toBe('lösning')
    expect(wrapper.find('[data-test="read-only-grade"]').text()).toBe('Godkänd')
    expect(wrapper.text()).toContain('Tidigare kommentar')
    expect(wrapper.findAll('[role="radio"]')).toHaveLength(0)
    expect(wrapper.find('textarea').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Spara')
    expect(wrapper.text()).not.toContain('Begär komplettering')
  })

  it('läraren ser bedömningsformuläret', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[data-test="read-only-grade"]').exists()).toBe(false)
    expect(wrapper.find('textarea').exists()).toBe(true)
    expect(wrapper.text()).toContain('Spara')
  })

  it('visar eleven, svaret, filerna och repot', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('h1').text()).toBe('Anna Andersson')
    expect(wrapper.find('strong').text()).toBe('lösning')

    const file = wrapper.find('a[href="https://files.example/f1"]')
    expect(file.text()).toContain('rapport.pdf')
    expect(file.text()).toContain('1,2 kB')
    expect(file.attributes('target')).toBe('_blank')

    expect(wrapper.text()).toContain('Repo')
    expect(wrapper.text()).toContain('anna/release-pipeline')
    expect(wrapper.text()).toContain('0123456')
    expect(wrapper.text()).toContain('Tester: 12/12')
    expect(wrapper.text()).toContain('Gröna')
    expect(wrapper.text()).toContain('Öppnat för komplettering')
  })

  describe('deadlines styr IG och Komplettera', () => {
    const past = '2020-01-01T10:00:00Z'
    const future = '2999-01-01T10:00:00Z'

    it('döljer IG och förklarar varför medan en deadline återstår, men erbjuder Komplettera', async () => {
      backend({scale: 'PassFailDistinction', dueAt: past, resubmitDueAt: [future]})

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(radios(wrapper)).toHaveLength(2)
      expect(radios(wrapper).join(' ')).not.toContain('IG')
      expect(wrapper.find('[data-test="review-fail-later"]').exists()).toBe(true)
      expect(wrapper.find('[data-test="review-request-work"]').exists()).toBe(true)
    })

    it('erbjuder IG men inte Komplettera när sista deadline har passerat', async () => {
      backend({scale: 'PassFail', dueAt: past, resubmitDueAt: ['2020-02-01T10:00:00Z']})

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(radios(wrapper)[0]).toContain('IG')
      expect(wrapper.find('[data-test="review-request-work"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="review-fail-later"]').exists()).toBe(false)
    })

    it('erbjuder allt när uppgiften saknar deadline', async () => {
      backend({scale: 'PassFail'})

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(radios(wrapper)).toHaveLength(2)
      expect(wrapper.find('[data-test="review-request-work"]').exists()).toBe(true)
    })

    it('kräver nytt val om ett tidigare IG inte längre är tillåtet', async () => {
      backend({
        scale: 'PassFail',
        dueAt: future,
        submission: {...gradedSubmission, grade: {...gradedSubmission.grade, level: 'Fail'}},
      })

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
      await button(wrapper, 'Spara').trigger('click')
      await flushPromises()

      expect(wrapper.text()).toContain('Välj ett betyg.')
      expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
    })

    it('håller Spara och nästa igång med bara G kvar', async () => {
      backend({
        scale: 'PassFail',
        dueAt: future,
        queue: [queued('s1', 'Anna'), queued('s2', 'Bo')],
      })

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
      await radiosButton(wrapper, 'G')
      await button(wrapper, 'Spara och nästa').trigger('click')
      await flushPromises()

      expect(sent('POST', '/api/submissions/s1/grade')).toMatchObject({level: 'Pass', outcome: 'Graded'})
    })
  })

  it('visar IG och G för skalan IG-G', async () => {
    backend({scale: 'PassFail'})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(radios(wrapper)).toHaveLength(2)
    expect(radios(wrapper)[0]).toContain('IG')
    expect(radios(wrapper)[0]).toContain('Icke godkänd')
    expect(radios(wrapper)[1]).toContain('G')
    expect(radios(wrapper)[1]).toContain('Godkänd')
  })

  it('visar IG, G och VG för skalan IG-VG', async () => {
    backend({scale: 'PassFailDistinction'})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(radios(wrapper)).toHaveLength(3)
    expect(radios(wrapper)[0]).toContain('IG')
    expect(radios(wrapper)[1]).toContain('G')
    expect(radios(wrapper)[2]).toContain('VG')
    expect(radios(wrapper)[2]).toContain('Väl godkänd')
  })

  it('visar bara återkoppling när uppgiften saknar betyg', async () => {
    backend({scale: 'None'})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.findAll('[role="radio"]')).toHaveLength(0)
    expect(wrapper.text()).toContain('Uppgiften har inget betyg, bara återkoppling.')
  })

  it('kräver ett betyg innan bedömningen publiceras', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await button(wrapper, 'Spara och tillbaka').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Välj ett betyg.')
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
  })

  it('visar uppgiften hopfälld och öppnar den med kriterier och checklista', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[data-test="review-task"]').exists()).toBe(true)
    expect(wrapper.text()).not.toContain('Lämna in länken')

    await wrapper.find('[data-test="review-task-toggle"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('[data-test="review-task"]').text()).toContain('Lämna in länken till repot.')
  })

  it('visar Att lämna in-listan i uppgiften', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('PassFail')]}
      }
      if (path === '/api/content/x1') {
        return {status: 200, body: {...content, bodyMd: 'Bygg pipelinen.\n\n- [ ] Länk till körning\n- [ ] README'}}
      }
      if (path === '/api/submissions/s1') {
        return {status: 200, body: baseSubmission}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
    await wrapper.find('[data-test="review-task-toggle"]').trigger('click')
    await flushPromises()

    const list = wrapper.find('[data-test="review-checklist"]')
    expect(list.findAll('li').map(text)).toEqual(['Länk till körning', 'README'])
    expect(list.text()).toContain('Att lämna in')
  })

  it('publicerar betyget och går tillbaka till Att bedöma när inget annat väntar', async () => {
    backend()

    const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await wrapper.findAll('[role="radio"]')[1].trigger('click')
    await wrapper.find('#feedback').setValue('Bra jobbat')
    await button(wrapper, 'Spara och tillbaka').trigger('click')
    await flushPromises()
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade'))
      .toEqual({feedback: 'Bra jobbat', level: 'Pass', outcome: 'Graded'})
    expect(router.currentRoute.value.name).toBe('grading')
    expect(wrapper.find('[data-test="grading-notice"]').text()).toBe('Bedömningen är sparad.')
  })

  describe('bedömningskön', () => {
    const queue = [
      baseSubmission,
      queued('s2', 'Örjan Berg'),
      queued('s3', 'Bo Carlsson'),
      queued('s4', 'Cecilia Dahl', 'Graded'),
      queued('s5', 'Dag Eriksson', 'NeedsWork'),
    ]

    it('visar hur många som är kvar och säger Spara och nästa', async () => {
      backend({queue})

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(text(wrapper.find('[data-test="review-remaining"]'))).toBe('3 kvar att bedöma')
      expect(text(wrapper.find('[data-test="review-save"]'))).toBe('Spara och nästa')
      expect(wrapper.find('[data-test="review-skip"]').exists()).toBe(true)
    })

    it('sparar och går till nästa inlämnade i listans ordning', async () => {
      backend({queue})

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      await wrapper.findAll('[role="radio"]')[1].trigger('click')
      await wrapper.find('[data-test="review-save"]').trigger('click')
      await flushPromises()
      await flushPromises()

      expect(sent('POST', '/api/submissions/s1/grade')).toMatchObject({level: 'Pass', outcome: 'Graded'})
      // Anna följs av Bo Carlsson, inte Örjan, och Cecilia (Bedömd) och Dag (Komplettera) hoppas över.
      expect(router.currentRoute.value.params.submissionId).toBe('s3')
      expect(wrapper.find('h1').text()).toBe('Bo Carlsson')
      expect(wrapper.find('[data-test="review-notice"]').text()).toBe('Bedömningen är sparad.')
    })

    it('begär komplettering och går också vidare', async () => {
      backend({queue})

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      await wrapper.find('#feedback').setValue('Komplettera.')
      await button(wrapper, 'Begär komplettering').trigger('click')
      await flushPromises()
      await flushPromises()

      expect(router.currentRoute.value.params.submissionId).toBe('s3')
    })

    it('säger bara Spara på den sista och går tillbaka till listan', async () => {
      backend({queue: [baseSubmission, queued('s4', 'Cecilia Dahl', 'Graded')]})

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(text(wrapper.find('[data-test="review-remaining"]'))).toBe('1 kvar att bedöma')
      expect(text(wrapper.find('[data-test="review-save"]'))).toBe('Spara och tillbaka')
      expect(wrapper.find('[data-test="review-skip"]').exists()).toBe(false)

      await wrapper.findAll('[role="radio"]')[1].trigger('click')
      await wrapper.find('[data-test="review-save"]').trigger('click')
      await flushPromises()
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('grading')
      expect(wrapper.find('[data-test="grading-notice"]').text()).toBe('Bedömningen är sparad.')
    })

    it('går vidare till nästa uppgift med obedömt arbete när kön är slut', async () => {
      backend({queue: [baseSubmission], otherAssignment: [queued('s7', 'Zeta Ek'), queued('s6', 'Ada Ek')]})

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
      await flushPromises()

      expect(text(wrapper.find('[data-test="review-save"]'))).toBe('Spara och nästa')

      await wrapper.findAll('[role="radio"]')[1].trigger('click')
      await wrapper.find('[data-test="review-save"]').trigger('click')
      await flushPromises()
      await flushPromises()

      expect(router.currentRoute.value.params.linkId).toBe('l2')
      expect(router.currentRoute.value.params.submissionId).toBe('s6')
    })

    it('hoppar över till nästa utan att spara', async () => {
      backend({queue})

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      await wrapper.find('[data-test="review-skip"]').trigger('click')
      await flushPromises()
      await flushPromises()

      expect(router.currentRoute.value.params.submissionId).toBe('s3')
      expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
    })

    it('frågar innan osparade ändringar kastas vid hoppa över', async () => {
      backend({queue})
      const confirm = vi.spyOn(window, 'confirm').mockReturnValueOnce(false).mockReturnValueOnce(true)

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
      await wrapper.find('#feedback').setValue('Halvskrivet')

      await wrapper.find('[data-test="review-skip"]').trigger('click')
      await flushPromises()
      expect(confirm).toHaveBeenCalledTimes(1)
      expect(router.currentRoute.value.params.submissionId).toBe('s1')

      await wrapper.find('[data-test="review-skip"]').trigger('click')
      await flushPromises()
      await flushPromises()
      expect(router.currentRoute.value.params.submissionId).toBe('s3')
      confirm.mockRestore()
    })

    it('frågar inte när inget är ändrat', async () => {
      backend({queue})
      const confirm = vi.spyOn(window, 'confirm')

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
      await wrapper.find('[data-test="review-skip"]').trigger('click')
      await flushPromises()

      expect(confirm).not.toHaveBeenCalled()
      confirm.mockRestore()
    })

    it('Komplettera och Bedömd står utanför kön och sparar tillbaka till listan', async () => {
      backend({queue: [gradedSubmission, queued('s2', 'Örjan Berg')], submission: gradedSubmission})

      const {wrapper, router} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(wrapper.find('[data-test="review-remaining"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="review-skip"]').exists()).toBe(false)
      expect(text(wrapper.find('[data-test="review-save"]'))).toBe('Spara')

      await wrapper.find('[data-test="review-save"]').trigger('click')
      await flushPromises()
      await flushPromises()

      expect(router.currentRoute.value.name).toBe('part')
    })

    it('mentorn ser varken formulär eller kö', async () => {
      backend({me: mentorAs(['c1']), queue})

      const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

      expect(wrapper.find('[data-test="review-save"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="review-remaining"]').exists()).toBe(false)
      expect(wrapper.find('[data-test="review-skip"]').exists()).toBe(false)
      expect(requests.some((request) => request.path === '/api/links/l1/submissions')).toBe(false)
    })
  })

  it('kräver en kommentar för IG och låser Spara tills den finns', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')
    await wrapper.findAll('[role="radio"]')[0].trigger('click')

    expect(wrapper.find('[data-test="review-fail-hint"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="review-save"]').attributes('disabled')).toBeDefined()

    await wrapper.find('#feedback').setValue('Testerna saknas.')

    expect(wrapper.find('[data-test="review-fail-hint"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="review-save"]').attributes('disabled')).toBeUndefined()
  })

  it('kräver en kommentar när eleven ska komplettera', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await button(wrapper, 'Begär komplettering').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('Skriv vad eleven ska komplettera.')
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(0)
  })

  it('begär komplettering med kommentaren och utan betyg', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    await wrapper.find('#feedback').setValue('Komplettera testerna.')
    await button(wrapper, 'Begär komplettering').trigger('click')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade'))
      .toEqual({feedback: 'Komplettera testerna.', level: null, outcome: 'NeedsWork'})
  })

  it('markerar som ej inlämnat med frivillig kommentar och utan betyg', async () => {
    backend({submission: {...baseSubmission, status: 'NotStarted', files: []}})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[data-test="review-panel"]').exists()).toBe(true)
    await button(wrapper, 'Markera som ej inlämnat').trigger('click')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade'))
      .toEqual({feedback: '', level: null, outcome: 'NotSubmitted'})
  })

  it('utan inlämning går det bara att markera ej inlämnat, inte betygsätta eller begära komplettering', async () => {
    backend({submission: {...baseSubmission, status: 'NotStarted', files: [], repo: null, bodyMd: null}})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[data-test="review-nothing-to-grade"]').text()).toContain('inte lämnat in något')
    expect(wrapper.find('[role="radiogroup"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="review-save"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="review-request-work"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="review-not-submitted"]').exists()).toBe(true)
  })

  it('ett ej inlämnat kan få ny kommentar men inget betyg', async () => {
    backend({submission: {
      ...baseSubmission,
      status: 'NotSubmitted',
      files: [],
      repo: null,
      bodyMd: null,
      grade: {gradedById: 'u0', level: null, feedback: 'Ingen inlämning', gradedAt: '2026-10-24T09:00:00Z'},
    }})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[data-test="review-save"]').exists()).toBe(false)
    await button(wrapper, 'Spara kommentaren').trigger('click')
    await flushPromises()

    expect(sent('POST', '/api/submissions/s1/grade'))
      .toEqual({feedback: 'Ingen inlämning', level: null, outcome: 'NotSubmitted'})
  })

  it('bedömning på plats går att betygsätta utan inlämning', async () => {
    backend({modes: ['OnSite'], submission: {...baseSubmission, status: 'NotStarted', files: [], repo: null, bodyMd: null}})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.find('[role="radiogroup"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="review-save"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="review-nothing-to-grade"]').exists()).toBe(false)
  })

  it('visar inte Markera som ej inlämnat när eleven lämnat in', async () => {
    backend()

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect(wrapper.text()).not.toContain('Markera som ej inlämnat')
  })

  it('fyller i en tidigare bedömning', async () => {
    backend({submission: gradedSubmission})

    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    expect((wrapper.find('#feedback').element as HTMLTextAreaElement).value).toBe('Tidigare kommentar')
    expect(wrapper.findAll('[role="radio"]')[1].attributes('aria-checked')).toBe('true')
    expect(wrapper.text()).toContain('Bedömd')
  })
})
