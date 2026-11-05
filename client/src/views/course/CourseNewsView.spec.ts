import {flushPromises} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const names: Record<string, string> = {u1: 'Anna Lärare', u2: 'Bo Elev'}

function news(
  linkId: string,
  contentId: string,
  title: string,
  publishAt: string,
  extra: Record<string, unknown> = {},
) {
  return {
    linkId,
    contentId,
    type: 'News',
    title,
    bodyMd: `${title} – brödtext`,
    authorId: 'u1',
    authorName: 'Anna Lärare',
    groupId: null,
    courseId: 'c1',
    moduleId: null,
    publishAt,
    dueAt: null,
    isPinned: false,
    ...extra,
  }
}

const pinned = news('l2', 'x2', 'Studiebesök', '2026-10-19T10:00:00Z', {isPinned: true})
const newest = news('l3', 'x3', 'Inlämning flyttad', '2026-10-10T10:00:00Z')
const oldest = news('l1', 'x1', 'Välkommen till kursen', '2026-10-01T10:00:00Z')

function comment(
  id: string,
  contentId: string,
  authorId: string,
  bodyMd: string,
  rights: {canEdit?: boolean, canDelete?: boolean} = {},
) {
  return {
    id,
    contentId,
    authorId,
    authorName: names[authorId],
    bodyMd,
    createdAt: '2026-10-19T11:00:00Z',
    canEdit: false,
    canDelete: false,
    ...rights,
  }
}

function backend(posts: unknown[], comments: Record<string, unknown[]> = {}) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/courses/c1/content') {
      return {status: 200, body: posts}
    }
    const oneComment = path.match(/^\/api\/content\/([^/]+)\/comments\/([^/]+)$/)
    if (oneComment && method === 'DELETE') {
      return {status: 204}
    }
    if (oneComment && method === 'PUT') {
      const sentBody = body as {bodyMd: string}
      return {status: 200, body: comment(oneComment[2]!, oneComment[1]!, 'u1', sentBody.bodyMd, {canEdit: true, canDelete: true})}
    }
    if (/^\/api\/content\/[^/]+\/mentionable$/.test(path)) {
      return {
        status: 200,
        body: [
          {id: '00000000-0000-0000-0000-0000000000b2', name: 'Bo Elev', avatarUrl: null, isTeacher: false},
          {id: '00000000-0000-0000-0000-0000000000c3', name: 'Cecilia Berg', avatarUrl: null, isTeacher: true},
        ],
      }
    }
    const withComments = path.match(/^\/api\/content\/([^/]+)\/comments$/)
    if (withComments) {
      const contentId = withComments[1]
      if (method === 'POST') {
        const sentBody = body as {bodyMd: string}
        return {
          status: 201,
          body: {
            id: `k-${contentId}`,
            contentId,
            authorId: 'u2',
            authorName: 'Bo Elev',
            bodyMd: sentBody.bodyMd,
            createdAt: '2026-10-20T10:00:00Z',
          },
        }
      }
      return {status: 200, body: comments[contentId] ?? []}
    }
    if (path === '/api/content/with-link' && method === 'POST') {
      return {
        status: 201,
        body: {
          content: {
            id: 'x9',
            authorId: 'u1',
            type: 'News',
            title: 'Nyhet',
            bodyMd: '',
            status: 'Published',
            createdAt: '2026-10-20T10:00:00Z',
            assignment: null,
          },
          link: {
            id: 'l9',
            contentId: 'x9',
            groupId: null,
            courseId: 'c1',
            moduleId: null,
            publishAt: '2026-10-20T10:00:00Z',
            dueAt: null,
            isPinned: false,
          },
        },
      }
    }
    const content = path.match(/^\/api\/content\/([^/]+)$/)
    if (content && method === 'PUT') {
      const sentBody = body as {title: string, bodyMd: string}
      return {
        status: 200,
        body: {
          id: content[1],
          authorId: 'u1',
          type: 'News',
          title: sentBody.title,
          bodyMd: sentBody.bodyMd,
          status: 'Published',
          createdAt: '2026-10-19T10:00:00Z',
          assignment: null,
        },
      }
    }
    const link = path.match(/^\/api\/content\/([^/]+)\/links\/([^/]+)$/)
    if (link && method === 'PUT') {
      const sentBody = body as {publishAt: string, dueAt: null, isPinned: boolean}
      return {
        status: 200,
        body: {
          id: link[2],
          contentId: link[1],
          groupId: null,
          courseId: 'c1',
          moduleId: null,
          ...sentBody,
        },
      }
    }
    return {status: 404}
  })
}

function titles(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']): string[] {
  return wrapper.findAll('[data-test="news-item"]')
    .map((item) => item.find('.font-semibold').text())
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

// Textfältet i nyhetsdialogen är en MarkdownEditor; markdown-läget har en textarea som går att fylla.
async function fillBody(selector: string, value: string) {
  dialogButton('Markdown').click()
  await flushPromises()
  await fill(selector, value)
}

async function submitDialogForm() {
  const form = document.querySelector('[role="dialog"] form') as HTMLFormElement
  form.dispatchEvent(new Event('submit'))
  await flushPromises()
  await flushPromises()
}

async function openNewsMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  await wrapper.find('[data-test="news-menu"]').trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

function newsAction(label: string): HTMLElement {
  const item = [...document.querySelectorAll('[role="menuitem"]')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!item) {
    throw new Error(`No menu item: ${label}`)
  }
  return item as HTMLElement
}

function dialogButton(label: string): HTMLElement {
  const button = [...document.querySelectorAll('[role="dialog"] button')]
    .find((candidate) => candidate.textContent?.trim() === label)
  if (!button) {
    throw new Error(`No dialog button: ${label}`)
  }
  return button as HTMLElement
}

describe('CourseNewsView', () => {
  it('listar nyheterna med fästa först och sedan nyast först, med namn och datum', async () => {
    backend([oldest, newest, pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(titles(wrapper)).toEqual(['Studiebesök', 'Inlämning flyttad', 'Välkommen till kursen'])
    const items = wrapper.findAll('[data-test="news-item"]')
    expect(items[0].text()).toContain('Fäst')
    expect(items[0].text()).toContain('mån 19 okt')
    expect(items[0].text()).toContain('Anna Lärare')
    expect(items[1].text()).not.toContain('Fäst')
  })

  it('visar första nyheten med text och kommentarer, och byter nyhet vid klick', async () => {
    backend([oldest, newest, pinned], {
      x2: [comment('k1', 'x2', 'u2', 'Ska bli kul!')],
      x3: [comment('k2', 'x3', 'u2', 'Tack för infon.')],
    })

    const {wrapper, router} = await renderApp('/courses/c1/news')

    expect(wrapper.find('article h2').text()).toBe('Studiebesök')
    expect(wrapper.text()).toContain('Studiebesök – brödtext')
    expect(wrapper.findAll('[data-test="comment"]').map((item) => item.text()))
      .toEqual([expect.stringContaining('Ska bli kul!')])
    expect(wrapper.find('h3#comments-heading').text()).toBe('1 kommentar')

    await wrapper.findAll('[data-test="news-item"]')[1].trigger('click')
    await flushPromises()
    await flushPromises()

    expect(router.currentRoute.value.query.post).toBe('l3')
    expect(wrapper.find('article h2').text()).toBe('Inlämning flyttad')
    expect(wrapper.findAll('[data-test="comment"]').map((item) => item.text()))
      .toEqual([expect.stringContaining('Tack för infon.')])
  })

  it('på mobil visar listan först, sedan bara artikeln med en länk tillbaka', async () => {
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: true,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }))
    backend([oldest, newest, pinned])

    const {wrapper, router} = await renderApp('/courses/c1/news')

    expect(wrapper.find('article').exists()).toBe(false)
    expect(titles(wrapper)).toHaveLength(3)

    await wrapper.findAll('[data-test="news-item"]')[1].trigger('click')
    await flushPromises()
    await flushPromises()

    expect(router.currentRoute.value.query.post).toBe('l3')
    expect(wrapper.find('article h2').text()).toBe('Inlämning flyttad')
    expect(wrapper.find('[data-test="news-item"]').exists()).toBe(false)

    router.back()
    await flushPromises()
    expect(wrapper.find('article').exists()).toBe(false)

    await wrapper.findAll('[data-test="news-item"]')[1].trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-test="back-to-list"]').text()).toBe('Alla nyheter')
    await wrapper.find('[data-test="back-to-list"]').trigger('click')
    await flushPromises()

    expect(wrapper.find('article').exists()).toBe(false)
    expect(titles(wrapper)).toHaveLength(3)
    vi.unstubAllGlobals()
  })

  it('skickar en kommentar och visar den direkt', async () => {
    backend([pinned], {x2: [comment('k1', 'x2', 'u2', 'Ska bli kul!')]})

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('#comment-body').setValue('Bra jobbat!')
    await wrapper.find('section[aria-labelledby="comments-heading"] form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content/x2/comments')).toEqual({bodyMd: 'Bra jobbat!'})
    expect(wrapper.findAll('[data-test="comment"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('Bra jobbat!')
    expect((wrapper.find('#comment-body').element as HTMLTextAreaElement).value).toBe('')
  })

  it('@ öppnar en lista att nämna någon ur, och omnämnandet skickas med id', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    const field = wrapper.find('#comment-body')
    await field.setValue('Hej @')
    await flushPromises()
    expect(wrapper.findAll('[data-test="mention-option"]').map((option) => option.find('.truncate').text()))
      .toEqual(['Bo Elev', 'Cecilia Berg'])

    await field.setValue('Hej @ber')
    await flushPromises()
    expect(wrapper.findAll('[data-test="mention-option"]').map((option) => option.find('.truncate').text()))
      .toEqual(['Cecilia Berg'])
    expect(wrapper.find('[data-test="mention-option"]').text()).toContain('Lärare')

    await field.trigger('keydown', {key: 'Enter'})
    await flushPromises()
    expect((field.element as HTMLTextAreaElement).value).toBe('Hej @Cecilia Berg ')
    expect(wrapper.find('[data-test="mention-list"]').exists()).toBe(false)

    await field.setValue('Hej @Cecilia Berg kan du titta?')
    await wrapper.find('section[aria-labelledby="comments-heading"] form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content/x2/comments')).toEqual({
      bodyMd: 'Hej @[Cecilia Berg](00000000-0000-0000-0000-0000000000c3) kan du titta?',
    })
    expect(wrapper.find('[data-test="comment-mention"]').text()).toBe('@Cecilia Berg')
  })

  it('pilarna väljer i listan och Escape stänger den utan att välja', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    const field = wrapper.find('#comment-body')
    await field.setValue('@')
    await flushPromises()
    await field.trigger('keydown', {key: 'ArrowDown'})
    expect(wrapper.find('[aria-selected="true"]').text()).toContain('Cecilia Berg')
    expect(field.attributes('aria-activedescendant')).toBe(wrapper.find('[aria-selected="true"]').attributes('id'))

    await field.trigger('keydown', {key: 'Escape'})
    expect(wrapper.find('[data-test="mention-list"]').exists()).toBe(false)
    expect((field.element as HTMLTextAreaElement).value).toBe('@')
  })

  it('en e-postadress eller ett namn som skrivits om blir ingen omnämning', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    const field = wrapper.find('#comment-body')
    await field.setValue('Mejla bo@skolan.se')
    await flushPromises()
    expect(wrapper.find('[data-test="mention-list"]').exists()).toBe(false)

    await field.setValue('@bo')
    await flushPromises()
    await field.trigger('keydown', {key: 'Enter'})
    await field.setValue('@Bosse hej')
    await wrapper.find('section[aria-labelledby="comments-heading"] form').trigger('submit')
    await flushPromises()

    expect(sent('POST', '/api/content/x2/comments')).toEqual({bodyMd: '@Bosse hej'})
  })

  it('visar omnämningar som namn och behåller dem när kommentaren ändras', async () => {
    const token = '@[Bo Elev](00000000-0000-0000-0000-0000000000b2)'
    backend([pinned], {x2: [comment('k1', 'x2', 'u1', `Tack ${token}!`, {canEdit: true, canDelete: true})]})

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.find('[data-test="comment"]').text()).toContain('Tack @Bo Elev!')
    expect(wrapper.find('[data-test="comment"]').text()).not.toContain('0000')

    await wrapper.find('[data-test="comment-menu"]').trigger('keydown', {key: 'Enter'})
    await flushPromises()
    newsAction('Redigera').click()
    await flushPromises()
    const field = wrapper.find('[data-test="comment-edit-form"] textarea')
    expect((field.element as HTMLTextAreaElement).value).toBe('Tack @Bo Elev!')

    await field.setValue('Tack igen @Bo Elev!')
    await wrapper.find('[data-test="comment-edit-form"]').trigger('submit')
    await flushPromises()

    expect(sent('PUT', '/api/content/x2/comments/k1')).toEqual({bodyMd: `Tack igen ${token}!`})
  })

  it('säger till när kommentaren är för lång och skickar den inte', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('#comment-body').setValue('a'.repeat(5001))
    await wrapper.find('section[aria-labelledby="comments-heading"] form').trigger('submit')
    await flushPromises()

    expect(wrapper.find('[data-test="text-limit"]').text().replace(/\s/g, ' '))
      .toBe('Texten är för lång: 5 001 tecken, högst 5 000.')
    expect(sent('POST', '/api/content/x2/comments')).toBeUndefined()
  })

  it('läraren tar bort en elevs kommentar efter att ha bekräftat', async () => {
    backend([pinned], {x2: [comment('k1', 'x2', 'u2', 'Olämpligt', {canDelete: true})]})

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('[data-test="comment-menu"]').trigger('keydown', {key: 'Enter'})
    await flushPromises()
    expect(document.querySelector('[data-test="comment-edit"]')).toBeNull()
    newsAction('Ta bort').click()
    await flushPromises()
    ;(document.querySelector('[data-test="comment-delete-confirm"]') as HTMLElement).click()
    await flushPromises()

    expect(requests.some((request) => request.method === 'DELETE'
      && request.path === '/api/content/x2/comments/k1')).toBe(true)
    expect(wrapper.findAll('[data-test="comment"]')).toHaveLength(0)
  })

  it('författaren ändrar sin kommentar på plats', async () => {
    backend([pinned], {x2: [comment('k1', 'x2', 'u1', 'Första', {canEdit: true, canDelete: true})]})

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('[data-test="comment-menu"]').trigger('keydown', {key: 'Enter'})
    await flushPromises()
    newsAction('Redigera').click()
    await flushPromises()
    await wrapper.find('#comment-edit-k1').setValue('Ändrad')
    await wrapper.find('[data-test="comment-edit-form"]').trigger('submit')
    await flushPromises()

    expect(sent('PUT', '/api/content/x2/comments/k1')).toEqual({bodyMd: 'Ändrad'})
    expect(wrapper.find('[data-test="comment-edit-form"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="comment"]').text()).toContain('Ändrad')
  })

  it('visar ingen meny på andras kommentarer utan rättigheter', async () => {
    backend([pinned], {x2: [comment('k1', 'x2', 'u2', 'Hej')]})

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.find('[data-test="comment-menu"]').exists()).toBe(false)
  })

  it('visar nyhetens text formaterad som markdown', async () => {
    backend([news('l1', 'x1', 'Kursstart', '2026-10-01T10:00:00Z', {bodyMd: 'Ligger under **Kurser**'})])

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.find('article strong').text()).toBe('Kurser')
  })

  it('ett tomt flöde visar hur man kommer igång', async () => {
    backend([])

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.text()).toContain('Inga nyheter än')
    expect(wrapper.find('[data-test="new-news"]').exists()).toBe(true)
  })

  it('en elev får inte publicera nyheter eller ändra dem', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Student', ['c1'], 'Bo Elev')}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/content') {
        return {status: 200, body: [pinned]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.findAll('[data-test="news-item"]')).toHaveLength(1)
    expect(wrapper.find('[data-test="new-news"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="news-menu"]').exists()).toBe(false)
  })

  it('en elev ser ingen Ny nyhet i ett tomt flöde', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Student', ['c1'], 'Bo Elev')}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.text()).toContain('Inga nyheter än')
    expect(wrapper.find('[data-test="new-news"]').exists()).toBe(false)
  })

  it('visar ett fel när nyheterna inte kan hämtas', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      return {status: 500, body: 'Server error'}
    })

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.text()).toContain('Kunde inte hämta nyheterna.')
    expect(wrapper.text()).not.toContain('Inga nyheter än')
  })

  it('dialogen publicerar en nyhet och fäster den i kursen', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('[data-test="new-news"]').trigger('click')
    await flushPromises()
    await fill('#news-title', 'Studiebesök på tisdag')
    await fillBody('#news-body', 'Vi ses i labbet klockan 10.')
    ;(document.querySelector('#news-pinned') as HTMLElement).click()
    await flushPromises()

    await submitDialogForm()

    expect(sent('POST', '/api/content/with-link')).toEqual({
      content: {
        type: 'News',
        title: 'Studiebesök på tisdag',
        bodyMd: 'Vi ses i labbet klockan 10.',
        status: 'Published',
      },
      link: {courseId: 'c1', isPinned: true},
    })
    expect(requests.filter((request) => request.method === 'POST')).toHaveLength(1)
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('publicerar inte en nyhet utan text', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('[data-test="new-news"]').trigger('click')
    await flushPromises()
    await fill('#news-title', 'Studiebesök på tisdag')
    await submitDialogForm()

    expect(document.body.textContent).toContain('Skriv något i nyheten.')
    expect(requests.filter((request) => request.method === 'POST')).toEqual([])
    expect(document.querySelector('[role="dialog"]')).not.toBeNull()
  })

  it('visar fel vid en tom nyhet i stället för att inte göra något', async () => {
    backend([pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    await wrapper.find('[data-test="new-news"]').trigger('click')
    await flushPromises()
    await submitDialogForm()

    expect(document.body.textContent).toContain('Skriv en rubrik.')
    expect(document.body.textContent).toContain('Skriv något i nyheten.')
    expect(requests.filter((request) => request.method === 'POST')).toEqual([])
  })

  it('redigerar nyheten och laddar om listan', async () => {
    backend([oldest, newest, pinned])

    const {wrapper} = await renderApp('/courses/c1/news')

    await openNewsMenu(wrapper)
    newsAction('Redigera').click()
    await flushPromises()
    await fill('#news-edit-title', 'Studiebesök flyttat')

    const form = document.querySelector('[role="dialog"] form') as HTMLFormElement
    form.dispatchEvent(new Event('submit'))
    await flushPromises()
    await flushPromises()

    expect(sent('PUT', '/api/content/x2')).toEqual({
      title: 'Studiebesök flyttat',
      bodyMd: 'Studiebesök – brödtext',
    })
    expect(requests.filter((request) => request.method === 'GET' && request.path === '/api/courses/c1/content'))
      .toHaveLength(2)
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('fäster nyheten och behåller publiceringstiden', async () => {
    backend([oldest, newest, pinned])

    const {wrapper} = await renderApp('/courses/c1/news?post=l3')

    await openNewsMenu(wrapper)
    newsAction('Fäst överst').click()
    await flushPromises()

    expect(sent('PUT', '/api/content/x3/links/l3')).toEqual({
      publishAt: '2026-10-10T10:00:00Z',
      dueAt: null,
      isPinned: true,
    })
  })

  it('raderar nyheten från kursen efter bekräftelse', async () => {
    let current = [oldest, newest, pinned]
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/content') {
        return {status: 200, body: current}
      }
      if (method === 'DELETE') {
        current = current.filter((post) => post.linkId !== 'l2')
        return {status: 204}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/news')

    await openNewsMenu(wrapper)
    newsAction('Ta bort').click()
    await flushPromises()
    expect(document.body.textContent).toContain('Nyheten tas bort från kursen.')

    dialogButton('Ta bort').click()
    await flushPromises()
    await flushPromises()

    expect(requests).toContainEqual({
      method: 'DELETE',
      path: '/api/content/x2/links/l2',
      body: undefined,
    })
    expect(document.querySelector('[role="dialog"]')).toBeNull()
    expect(titles(wrapper)).toEqual(['Inlämning flyttad', 'Välkommen till kursen'])
  })

  it('visar ett fel när nyheten inte kan tas bort', async () => {
    mockApi((method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [course]}
      }
      if (path === '/api/courses/c1/content') {
        return {status: 200, body: [pinned]}
      }
      if (method === 'DELETE') {
        return {status: 500, body: 'Server error'}
      }
      return {status: 200, body: []}
    })

    const {wrapper} = await renderApp('/courses/c1/news')

    await openNewsMenu(wrapper)
    newsAction('Ta bort').click()
    await flushPromises()
    dialogButton('Ta bort').click()
    await flushPromises()

    expect(wrapper.text()).toContain('Kunde inte ta bort nyheten.')
  })
})
