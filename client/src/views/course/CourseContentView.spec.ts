import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

// Eleverna får elevvyn på samma sida. Den skrivskyddade listan visas för admin, som kan läsa kursen men inte ändra den.
const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

function week(id: string, title: string, content: unknown[] = [], publishAt: string | null = null) {
  return {id, courseId: 'c1', title, description: '', position: 1, publishAt, content}
}

const submission = {
  linkId: 'l1',
  contentId: 'x1',
  type: 'Assignment',
  gradeScale: 'PassFailDistinction',
  title: 'Release-pipeline',
  status: 'Published',
  groupId: null,
  publishAt: '2026-01-01T08:00:00Z',
  dueAt: '2026-10-23T21:59:00Z',
  resubmitDueAt: [],
  isPinned: false,
  blockedBy: [],
  hasStudentActivity: false,
}
const exercise = {...submission, linkId: 'l2', gradeScale: 'None', title: 'Docker-övning', dueAt: null}
const draft = {
  ...submission,
  linkId: 'l3',
  contentId: 'x3',
  type: 'Theory',
  gradeScale: null,
  title: 'Changelog',
  status: 'Draft',
  dueAt: null,
}

function backend(modules: unknown[]) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: modules}
    }
    if (path === '/api/courses/c1/modules/publish') {
      return {status: 200, body: {weekCount: 1, partCount: 1}}
    }
    return {status: 200, body: []}
  })
}

async function openPartMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], title: string) {
  await wrapper.find(`[aria-label="Fler val för ${title}"]`).trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

function partAction(label: string): HTMLElement {
  const item = [...document.querySelectorAll('[role="menuitem"]')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!item) {
    throw new Error(`No menu item: ${label}`)
  }
  return item as HTMLElement
}

async function openWeekMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  await wrapper.find('[aria-label="Fler val för veckan"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

describe('CourseContentView', () => {
  it('visar en vecka med bara utkast som utkast med planerat datum och utan märke per del', async () => {
    backend([week('m1', 'Vecka 1', [draft], '2099-11-16T05:00:00Z')])
    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.find('[data-test="week-planned"]').text()).toContain('Planerad')
    expect(wrapper.text()).not.toContain('Schemalagd')
  })

  it('Publicera veckan skickar veckan och säger när eleverna ser den', async () => {
    backend([week('m1', 'Vecka 1', [draft], '2099-11-16T05:00:00Z')])
    const {wrapper} = await renderApp('/courses/c1/content')

    await wrapper.find('[data-test="publish-week"]').trigger('click')
    await flushPromises()

    expect(sent('POST', '/api/courses/c1/modules/publish')).toEqual({moduleIds: ['m1']})
    expect(wrapper.text()).toContain('Vecka 1 publicerades. Eleverna ser veckan')
  })

  it('Publicera alla veckor listar veckorna med utkast och publicerar dem', async () => {
    backend([week('m1', 'Vecka 1', [draft]), week('m2', 'Vecka 2', [submission])])
    const {wrapper} = await renderApp('/courses/c1/content')

    await wrapper.find('[data-test="publish-all"]').trigger('click')
    await flushPromises()

    const dialog = document.querySelector('[role="dialog"]') as HTMLElement
    expect(dialog.querySelectorAll('[data-test="publish-week"]')).toHaveLength(1)
    ;(dialog.querySelector('[data-test="confirm-publish"]') as HTMLElement).click()
    await flushPromises()

    expect(sent('POST', '/api/courses/c1/modules/publish')).toEqual({moduleIds: ['m1']})
  })

  it('utan utkast finns ingen publiceringsknapp', async () => {
    backend([week('m1', 'Vecka 1', [submission])])
    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.find('[data-test="publish-all"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="publish-week"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="copy-weeks"]').exists()).toBe(true)
  })

  it('visar veckorna med sina delar, typ, deadline och status', async () => {
    backend([week('m1', 'Vecka 1', [submission, exercise, draft]), week('m2', 'Vecka 2')])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.findAll('section h2').map((heading) => heading.text())).toEqual(['Vecka 1', 'Vecka 2'])
    const items = wrapper.findAll('[data-test="module-item"]').map((item) => item.text())
    expect(items[0]).toContain('Release-pipeline')
    expect(items[0]).toContain('Inlämning · fre 23 okt')
    // Publicerad är normalläget, så en publicerad del har inget eget märke.
    expect(items[0]).not.toContain('Publicerad')
    expect(items[1]).toContain('Övning')
    expect(items[2]).toContain('Teori')
    expect(items[2]).toContain('Utkast')
  })

  it('en kurs utan veckor visar hur man kommer igång', async () => {
    backend([])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.text()).toContain('Inga veckor än')
  })

  it('visar veckans låsningar och hur många delar en del låser upp', async () => {
    backend([week('m1', 'Vecka 1', [
      {...submission, blockedBy: []},
      {...exercise, blockedBy: ['l1']},
    ])])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.text()).toContain('Låser upp 1 del')
    expect(wrapper.text()).toContain('Låst av 1 del')
  })

  it('flytta ner skickar den nya ordningen', async () => {
    backend([week('m1', 'Vecka 1'), week('m2', 'Vecka 2')])
    const {wrapper} = await renderApp('/courses/c1/content')

    await wrapper.findAll('[aria-label="Fler val för veckan"]')[0].trigger('keydown', {key: 'Enter'})
    await flushPromises()
    const moveDown = [...document.querySelectorAll('[role="menuitem"]')].find((item) => item.textContent?.includes('Flytta ner'))
    ;(moveDown as HTMLElement).click()
    await flushPromises()

    expect(sent('PUT', '/api/courses/c1/modules/order')).toEqual({moduleIds: ['m2', 'm1']})
  })

  it('lägg till del går till formuläret för veckan', async () => {
    backend([week('m1', 'Vecka 1')])

    const {wrapper} = await renderApp('/courses/c1/content')
    await flushPromises()

    // Knappen är en direkt länk till formuläret, utan mellansteg.
    const add = wrapper.find('[data-test="add-part"]')
    expect(add.text()).toBe('Lägg till del')
    expect(add.attributes('href')).toBe('/courses/c1/modules/m1/new')
  })

  it('publicerar och avpublicerar från raden', async () => {
    backend([week('m1', 'Vecka 1', [draft, submission])])
    const {wrapper} = await renderApp('/courses/c1/content')

    await openPartMenu(wrapper, 'Changelog')
    partAction('Publicera').click()
    await flushPromises()
    expect(sent('PUT', '/api/content/x3/status')).toEqual({status: 'Published'})

    await openPartMenu(wrapper, 'Release-pipeline')
    partAction('Avpublicera').click()
    await flushPromises()
    expect(sent('PUT', '/api/content/x1/status')).toEqual({status: 'Draft'})
  })

  it('flyttar en del upp och skickar den nya ordningen', async () => {
    backend([week('m1', 'Vecka 1', [submission, exercise, draft])])
    const {wrapper} = await renderApp('/courses/c1/content')

    await openPartMenu(wrapper, 'Docker-övning')
    partAction('Flytta upp').click()
    await flushPromises()

    expect(sent('PUT', '/api/courses/c1/modules/m1/content/order'))
      .toEqual({linkIds: ['l2', 'l1', 'l3']})
  })

  it('raderar en del efter bekräftelse', async () => {
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', 'Vecka 1', [submission])]}
      }
      if (method === 'DELETE') {
        return {status: 204}
      }
      return {status: 200, body: []}
    })
    const {wrapper} = await renderApp('/courses/c1/content')

    await openPartMenu(wrapper, 'Release-pipeline')
    partAction('Ta bort').click()
    await flushPromises()
    expect(document.body.textContent).toContain('Release-pipeline tas bort från veckan.')

    const confirm = [...document.querySelectorAll('[role="dialog"] button')]
      .find((button) => button.textContent?.trim() === 'Ta bort')
    ;(confirm as HTMLElement).click()
    await flushPromises()

    expect(requests).toContainEqual({
      method: 'DELETE',
      path: '/api/content/x1/links/l1',
      body: undefined,
    })
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('visar varför en del inte får raderas', async () => {
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', 'Vecka 1', [submission])]}
      }
      if (method === 'DELETE') {
        return {status: 409, body: 'the part has student activity, files or a repository and cannot be deleted'}
      }
      return {status: 200, body: []}
    })
    const {wrapper} = await renderApp('/courses/c1/content')

    await openPartMenu(wrapper, 'Release-pipeline')
    partAction('Ta bort').click()
    await flushPromises()
    const confirm = [...document.querySelectorAll('[role="dialog"] button')]
      .find((button) => button.textContent?.trim() === 'Ta bort')
    ;(confirm as HTMLElement).click()
    await flushPromises()

    expect(document.body.textContent).toContain('Delen har elevaktivitet, filer eller ett repo och kan inte tas bort.')
  })

  it('titeln öppnar delen och redigera går till redigeringssidan', async () => {
    backend([week('m1', 'Vecka 1', [submission])])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.find('a[href="/courses/c1/parts/l1"]').text()).toBe('Release-pipeline')
    await openPartMenu(wrapper, 'Release-pipeline')
    const edit = partAction('Redigera')
    expect(edit.tagName).toBe('A')
    expect(edit.getAttribute('href')).toBe('/courses/c1/modules/m1/content/l1/edit')
  })

  it('inlämningar-menyn går till uppgiftens inlämningar', async () => {
    backend([week('m1', 'Vecka 1', [submission])])

    const {wrapper} = await renderApp('/courses/c1/content')
    await openPartMenu(wrapper, 'Release-pipeline')
    const item = partAction('Inlämningar')

    expect(item.tagName).toBe('A')
    expect(item.getAttribute('href')).toBe('/courses/c1/assignments/l1')
  })

  it('teoridelar har ingen inlämningsmeny', async () => {
    backend([week('m1', 'Vecka 1', [draft])])

    const {wrapper} = await renderApp('/courses/c1/content')
    await openPartMenu(wrapper, 'Changelog')
    const labels = [...document.querySelectorAll('[role="menuitem"]')]
      .map((item) => item.textContent?.trim())

    expect(labels).not.toContain('Inlämningar')
  })

  it('visar antal inlämnade på en inlämning, men inte på teori', async () => {
    backend([week('m1', 'Vecka 1', [{...submission, submittedCount: 6, studentCount: 28}, draft])])

    const {wrapper} = await renderApp('/courses/c1/content')

    const items = wrapper.findAll('[data-test="module-item"]')
    expect(items[0]!.text()).toContain('Inlämning · fre 23 okt · 6/28 inlämnade')
    const count = items[0]!.find('[data-test="submission-count"]')
    expect(count.attributes('href')).toBe('/courses/c1/assignments/l1')
    expect(items[1]!.find('[data-test="submission-count"]').exists()).toBe(false)
    expect(items[1]!.text()).not.toContain('inlämnade')
  })

  it('admin ser inga antal inlämnade', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Admin', [])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', 'Vecka 1', [{...submission, submittedCount: null, studentCount: null}])]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.text()).toContain('Release-pipeline')
    expect(wrapper.text()).not.toContain('inlämnade')
  })

  it('admin läser kursen, med utkast, men utan redigera, publicera eller bedöma', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Admin', [])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        const handIn = {...submission, completionRule: 'Submitted'}
        return {status: 200, body: [week('m1', 'Vecka 1', [handIn, draft])]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/content')

    const items = wrapper.findAll('[data-test="module-item"]').map((item) => item.text())
    expect(items[0]).toContain('Release-pipeline')
    expect(items[1]).toContain('Changelog')
    expect(items[1]).toContain('Utkast')
    expect(wrapper.find('[data-test="add-module"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="add-part"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="Fler val för veckan"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="Fler val för Release-pipeline"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="submissions-link"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="submission-count"]').exists()).toBe(false)
    expect(wrapper.find('a[href="/courses/c1/modules/m1/content/l1/edit"]').exists()).toBe(false)
  })

  it('en lärare ser knapparna som skapar veckor och delar', async () => {
    backend([week('m1', 'Vecka 1', [submission])])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.find('[data-test="add-module"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Fler val för veckan"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Fler val för Release-pipeline"]').exists()).toBe(true)
  })

  it('admin ser delarna utan redigeringskontroller', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Admin', [])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', 'Vecka 1', [submission])]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.findAll('[data-test="module-item"]')).toHaveLength(1)
    expect(wrapper.text()).toContain('Release-pipeline')
    expect(wrapper.find('[data-test="add-module"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="add-part"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="Fler val för veckan"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="Fler val för Release-pipeline"]').exists()).toBe(false)
    expect(wrapper.find('a[href="/courses/c1/modules/m1/content/l1/edit"]').exists()).toBe(false)
  })

  it('admin ser varken Flytta till vecka eller veckomenyns namn och radering', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Admin', [])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', 'Vecka 1', [submission]), week('m2', 'Vecka 2')]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.text()).toContain('Release-pipeline')
    expect(wrapper.find('[aria-label="Fler val för veckan"]').exists()).toBe(false)
    expect(wrapper.find('[aria-label="Fler val för Release-pipeline"]').exists()).toBe(false)
    const labels = [...document.querySelectorAll('[role="menuitem"]')]
      .map((item) => item.textContent?.trim())
    expect(labels).not.toContain('Flytta till vecka…')
    expect(labels).not.toContain('Byt namn')
    expect(labels).not.toContain('Ta bort vecka')
  })

  it('visar veckans publiceringsdatum i veckomärket', async () => {
    // En vecka fram, så att veckan alltid är schemalagd när testet körs.
    backend([week('m1', 'Vecka 1', [submission], new Date(Date.now() + 7 * 86_400_000).toISOString())])

    const {wrapper} = await renderApp('/courses/c1/content')

    expect(wrapper.text()).toContain('Schemalagd')
  })

  it('flyttar en del till en annan vecka och visar att beroenden togs bort', async () => {
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {
          status: 200,
          body: [week('m1', 'Vecka 1', [{
            ...submission,
            blockedBy: ['l2'],
          }]), week('m2', 'Vecka 2', [exercise])],
        }
      }
      if (method === 'PUT' && path === '/api/content/x1/links/l1/module') {
        return {
          status: 200,
          body: {linkId: 'l1', moduleId: 'm2', position: 1, affectedLinkIds: ['l2']},
        }
      }
      return {status: 200, body: []}
    })
    const {wrapper} = await renderApp('/courses/c1/content')

    await openPartMenu(wrapper, 'Release-pipeline')
    partAction('Flytta till vecka…').click()
    await flushPromises()
    expect(document.body.textContent).toContain('Beroenden tas bort')

    document.querySelector<HTMLElement>('[data-test="move-target-m2"]')!.click()
    await flushPromises()
    document.querySelector<HTMLElement>('[data-test="confirm-move"]')!.click()
    await flushPromises()

    expect(sent('PUT', '/api/content/x1/links/l1/module')).toEqual({moduleId: 'm2'})
    expect(wrapper.text()).toContain('1 beroenden togs bort')
  })

  it('blockerar flytt när eleven har aktivitet', async () => {
    backend([week('m1', 'Vecka 1', [{
      ...submission,
      hasStudentActivity: true,
    }]), week('m2', 'Vecka 2')])
    const {wrapper} = await renderApp('/courses/c1/content')

    await openPartMenu(wrapper, 'Release-pipeline')
    partAction('Flytta till vecka…').click()
    await flushPromises()

    expect(document.querySelector('[data-test="move-activity-warning"]')?.textContent)
      .toContain('kan därför inte flyttas')
    expect(document.querySelector<HTMLButtonElement>('[data-test="confirm-move"]')?.disabled).toBe(true)
  })

  it('byter namn på en vecka', async () => {
    backend([week('m1', 'Vecka 1')])
    const {wrapper} = await renderApp('/courses/c1/content')

    await openWeekMenu(wrapper)
    partAction('Byt namn').click()
    await flushPromises()
    const title = document.querySelector<HTMLInputElement>('[data-test="edit-module-title"]')!
    title.value = 'Vecka 1 – Intro'
    title.dispatchEvent(new Event('input'))
    await flushPromises()
    document.querySelector<HTMLElement>('[data-test="save-module"]')!.click()
    await flushPromises()

    expect(sent('PUT', '/api/courses/c1/modules/m1')).toEqual({
      title: 'Vecka 1 – Intro',
      description: null,
      publishAt: null,
    })
  })

  it('raderar en vecka först när titeln skrivs exakt', async () => {
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/modules') {
        return {status: 200, body: [week('m1', 'Vecka 1', [submission])]}
      }
      if (method === 'DELETE') {
        return {status: 204}
      }
      return {status: 200, body: []}
    })
    const {wrapper} = await renderApp('/courses/c1/content')

    await openWeekMenu(wrapper)
    partAction('Ta bort vecka').click()
    await flushPromises()

    expect(document.querySelector('[data-test="delete-week-published"]')?.textContent)
      .toContain('Publicerad inlämning')
    expect(document.querySelector<HTMLButtonElement>('[data-test="confirm-delete-week"]')?.disabled)
      .toBe(true)

    const confirmTitle = document.querySelector<HTMLInputElement>('[data-test="delete-week-confirm"]')!
    confirmTitle.value = 'Vecka 1'
    confirmTitle.dispatchEvent(new Event('input'))
    await flushPromises()
    document.querySelector<HTMLElement>('[data-test="confirm-delete-week"]')!.click()
    await flushPromises()

    expect(requests).toContainEqual({
      method: 'DELETE',
      path: '/api/courses/c1/modules/m1',
      body: undefined,
    })
  })

  it('blockerar radering av en vecka med elevaktivitet', async () => {
    backend([week('m1', 'Vecka 1', [{
      ...submission,
      hasStudentActivity: true,
    }])])
    const {wrapper} = await renderApp('/courses/c1/content')

    await openWeekMenu(wrapper)
    partAction('Ta bort vecka').click()
    await flushPromises()

    expect(document.querySelector('[data-test="delete-week-activity"]')?.textContent)
      .toContain('Veckan kan därför inte tas bort')
    expect(document.querySelector<HTMLButtonElement>('[data-test="confirm-delete-week"]')?.disabled)
      .toBe(true)
  })
})
