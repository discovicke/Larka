import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, sent} from '@/test/mockApi'
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
    if (path === '/api/content' && method === 'POST') {
      return {
        status: 201,
        body: {
          id: 'x9',
          authorId: 'u1',
          type: 'News',
          title: 'Nyhet',
          bodyMd: '',
          status: 'Published',
          createdAt: '2026-10-20T10:00:00Z',
          assignment: null,
        },
      }
    }
    if (path === '/api/content/x9/links' && method === 'POST') {
      return {
        status: 201,
        body: {
          id: 'l9',
          contentId: 'x9',
          groupId: null,
          courseId: 'c1',
          moduleId: null,
          publishAt: '2026-10-20T10:00:00Z',
          dueAt: null,
          isPinned: false,
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

  it('ett tomt flöde visar hur man kommer igång', async () => {
    backend([])

    const {wrapper} = await renderApp('/courses/c1/news')

    expect(wrapper.text()).toContain('Inga nyheter än')
    expect(wrapper.find('[data-test="new-news"]').exists()).toBe(true)
  })

  it('visar ett fel när nyheterna inte kan hämtas', async () => {
    mockApi((_method, path) => {
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
    await fill('#news-body', 'Vi ses i labbet klockan 10.')
    ;(document.querySelector('#news-pinned') as HTMLElement).click()
    await flushPromises()

    const form = document.querySelector('[role="dialog"] form') as HTMLFormElement
    form.dispatchEvent(new Event('submit'))
    await flushPromises()
    await flushPromises()

    expect(sent('POST', '/api/content')).toEqual({
      type: 'News',
      title: 'Studiebesök på tisdag',
      bodyMd: 'Vi ses i labbet klockan 10.',
      status: 'Published',
    })
    expect(sent('POST', '/api/content/x9/links')).toEqual({courseId: 'c1', isPinned: true})
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })
})
