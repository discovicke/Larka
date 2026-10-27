import {afterEach, beforeEach, describe, expect, it} from 'vitest'
import {createMemoryHistory} from 'vue-router'

import {createAppRouter} from './router'

import {signInForTests, signOutForTests} from '@/test/auth'

async function open(path: string) {
  const router = createAppRouter(createMemoryHistory())
  await router.push(path)
  return router.currentRoute.value
}

describe('router', () => {
  beforeEach(() => {
    signInForTests()
  })

  it.each([
    ['/', 'today'],
    ['/calendar', 'calendar'],
    ['/inbox', 'inbox'],
    ['/material', 'material'],
    ['/grading', 'grading'],
    ['/groups', 'groups'],
    ['/settings', 'settings'],
    ['/courses/devops/content', 'course-content'],
    ['/courses/devops/news', 'course-news'],
    ['/courses/devops/staff', 'course-staff'],
    ['/courses/devops/about', 'course-about'],
    ['/courses/devops/modules/1/new', 'create-content'],
    ['/courses/devops/assignments/l1', 'assignment-submissions'],
    ['/courses/devops/assignments/l1/submissions/s1', 'submission-review'],
    ['/dev', 'dev'],
  ])('%s öppnar %s', async (path, routeName) => {
    const route = await open(path)

    expect(route.name).toBe(routeName)
  })

  it('kurssidan öppnar fliken Innehåll', async () => {
    const route = await open('/courses/devops')

    expect(route.name).toBe('course-content')
    expect(route.params.courseId).toBe('devops')
  })

  it('okänd sökväg ger sidan som inte finns', async () => {
    const route = await open('/finns-inte')

    expect(route.name).toBe('not-found')
  })

  it('sätter titeln i webbläsarfliken', async () => {
    await open('/calendar')

    expect(document.title).toBe('Kalender · RebornLMS')
  })
})

describe('router med inloggning', () => {
  afterEach(() => {
    signOutForTests()
  })

  it('utloggad skickas till inloggningen med sidan man ville till', async () => {
    signOutForTests()

    const route = await open('/groups?sort=namn')

    expect(route.name).toBe('login')
    expect(route.query.redirect).toBe('/groups?sort=namn')
  })

  it('inloggad kommer till sidorna', async () => {
    signInForTests()

    const route = await open('/groups')

    expect(route.name).toBe('groups')
  })

  it('inloggningssidan är publik', async () => {
    signOutForTests()

    const route = await open('/login')

    expect(route.name).toBe('login')
  })

  it('inloggad skickas från inloggningssidan till startsidan', async () => {
    signInForTests()

    const route = await open('/login')

    expect(route.name).toBe('today')
  })
})
