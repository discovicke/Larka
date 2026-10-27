import {describe, expect, it} from 'vitest'
import {createMemoryHistory} from 'vue-router'

import {createAppRouter} from './router'

async function open(path: string) {
  const router = createAppRouter(createMemoryHistory())
  await router.push(path)
  return router.currentRoute.value
}

describe('router', () => {
  it.each([
    ['/', 'today'],
    ['/calendar', 'calendar'],
    ['/inbox', 'inbox'],
    ['/grading', 'grading'],
    ['/groups', 'groups'],
    ['/courses/devops/content', 'course-content'],
    ['/courses/devops/news', 'course-news'],
    ['/courses/devops/channels', 'course-channels'],
    ['/courses/devops/staff', 'course-staff'],
    ['/courses/devops/about', 'course-about'],
    ['/courses/devops/modules/1/new', 'create-content'],
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
