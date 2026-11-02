import {afterEach, beforeEach, describe, expect, it} from 'vitest'
import {createMemoryHistory} from 'vue-router'

import {createAppRouter, type Breadcrumb} from './router'

import {useCourses} from '@/composables/useCourses'
import {useCrumbLabels} from '@/composables/useCrumbLabels'

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
    ['/courses/devops/about', 'course-about'],
    ['/courses/devops/modules/1/new', 'create-content'],
    ['/courses/devops/parts/l1', 'part'],
    ['/courses/devops/assignments/l1', 'part'],
    ['/courses/devops/assignments/l1/submissions/s1', 'submission-review'],
  ])('%s öppnar %s', async (path, routeName) => {
    const route = await open(path)

    expect(route.name).toBe(routeName)
  })

  it('personalsidan och länkar till course-staff går till Om vid personalen', async () => {
    const byPath = await open('/courses/devops/staff')
    expect(byPath.name).toBe('course-about')
    expect(byPath.hash).toBe('#personal')

    const router = createAppRouter(createMemoryHistory())
    await router.push({name: 'course-staff', params: {courseId: 'devops'}})
    expect(router.currentRoute.value.name).toBe('course-about')
    expect(router.currentRoute.value.hash).toBe('#personal')
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

    expect(document.title).toBe('Kalender · Lärka')
  })

  it('den gamla sidan Program och klasser skickar vidare till Alla grupper', async () => {
    const route = await open('/settings/groups')

    expect(route.name).toBe('groups')
    expect(route.path).toBe('/groups')
  })
})

function trail(route: Awaited<ReturnType<typeof open>>): Breadcrumb[] {
  return route.meta.breadcrumbs?.(route) ?? []
}

const kurser = {label: 'Kurser', to: {name: 'groups', query: {filter: 'courses'}}}
const devops = {label: 'DevOps', to: {name: 'course-content', params: {courseId: 'c1'}}}
const uppgift = {label: 'Uppgift', to: {name: 'part', params: {courseId: 'c1', linkId: 'l1'}}}
const inlamningar = {label: 'Inlämningar', to: {name: 'assignment-submissions', params: {courseId: 'c1', linkId: 'l1'}}}
const innehall = {label: 'Innehåll', to: {name: 'course-content', params: {courseId: 'c1'}}}

describe('brödsmulor', () => {
  beforeEach(() => {
    signInForTests()
    useCourses().courses.value = [{id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps', description: '', points: null, startsOn: null, endsOn: null}]
  })

  afterEach(() => {
    useCourses().courses.value = []
  })

  it.each([
    ['kursens innehåll', '/courses/c1/content', [kurser, devops]],
    ['kursens nyheter', '/courses/c1/news', [kurser, devops, {label: 'Nyheter'}]],
    ['om kursen', '/courses/c1/about', [kurser, devops, {label: 'Om'}]],
    ['ny del', '/courses/c1/modules/m1/new', [kurser, devops, innehall, {label: 'Ny del'}]],
    ['redigera del', '/courses/c1/modules/m1/content/l1/edit', [kurser, devops, innehall, {label: 'Redigera del'}]],
    ['en del', '/courses/c1/parts/l1', [kurser, devops, {label: 'Del'}]],
    ['en inlämning', '/courses/c1/assignments/l1/submissions/s1', [kurser, devops, uppgift, inlamningar, {label: 'Inlämning'}]],
    ['en grupp', '/groups/g1', [{label: 'Alla grupper', to: {name: 'groups'}}, {label: 'Grupp'}]],
  ])('%s: spåret leder tillbaka till listan', async (_name, path, expected) => {
    expect(trail(await open(path))).toEqual(expected)
  })

  it('en öppnad del i elevvyn blir sista brödsmula', async () => {
    expect(trail(await open('/courses/c1/content?del=l1')))
      .toEqual([kurser, devops, {label: 'Del'}])
  })

  it('Kurser länkar till Alla grupper filtrerat på kurser', async () => {
    const [first] = trail(await open('/courses/c1/news'))
    const router = createAppRouter(createMemoryHistory())

    expect(router.resolve(first.to!).fullPath).toBe('/groups?filter=courses')
  })

  it('visar namnen sidorna har satt', async () => {
    const {setCrumbLabel} = useCrumbLabels()
    setCrumbLabel('group:g7', 'NET25')
    setCrumbLabel('assignment:l9', 'Release-pipeline')
    setCrumbLabel('student:s9', 'Bo Elev')

    expect(trail(await open('/groups/g7')).map((crumb) => crumb.label)).toEqual(['Alla grupper', 'NET25'])
    expect(trail(await open('/courses/c1/assignments/l9/submissions/s9')).map((crumb) => crumb.label))
      .toEqual(['Kurser', 'DevOps', 'Release-pipeline', 'Inlämningar', 'Bo Elev'])
  })

  it('sidor utan eget spår behåller sitt namn', async () => {
    expect(trail(await open('/grading'))).toEqual([])
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
