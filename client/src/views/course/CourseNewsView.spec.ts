import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const course = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const teacher = {id: 'u1', schoolId: 's1', name: 'Anna Lärare', email: 'anna@example.com', role: 'Teacher'}
const student = {id: 'u2', schoolId: 's1', name: 'Bo Elev', email: 'bo@example.com', role: 'Student'}

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

function comment(id: string, contentId: string, authorId: string, bodyMd: string) {
  return {id, contentId, authorId, bodyMd, createdAt: '2026-10-19T11:00:00Z'}
}

function backend(posts: unknown[], comments: Record<string, unknown[]> = {}) {
  mockApi((method, path, body) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [course]}
    }
    if (path === '/api/users') {
      return {status: 200, body: [teacher, student]}
    }
    if (path === '/api/courses/c1/content') {
      return {status: 200, body: posts}
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
      if (path === '/api/users') {
        return {status: 200, body: [teacher, student]}
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
      if (path === '/api/users') {
        return {status: 200, body: [teacher, student]}
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
      if (path === '/api/users') {
        return {status: 200, body: [teacher]}
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

    expect(document.body.textContent).toContain('Skriv en text.')
    expect(requests.filter((request) => request.method === 'POST')).toEqual([])
    expect(document.querySelector('[role="dialog"]')).not.toBeNull()
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
      if (path === '/api/users') {
        return {status: 200, body: [teacher, student]}
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
      if (path === '/api/users') {
        return {status: 200, body: [teacher, student]}
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
