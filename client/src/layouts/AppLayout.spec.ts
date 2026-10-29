import {flushPromises} from '@vue/test-utils'
import {afterEach, beforeEach, describe, expect, it} from 'vitest'
import {defineComponent, h} from 'vue'
import {createMemoryHistory, type Router} from 'vue-router'

import PageActions from '@/components/layout/PageActions.vue'
import {auth} from '@/lib/auth'
import {createAppRouter} from '@/router'
import {signOutForTests} from '@/test/auth'
import {mockApi, requests} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

async function render(path: string, router?: Router) {
  return (await renderApp(path, router)).wrapper
}

const devOps = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}
const teacher = {id: 'u1', schoolId: 's1', name: 'Anna Lärare', email: 'anna@example.com', role: 'Teacher'}
const student = {id: 'u2', schoolId: 's1', name: 'Bo Elev', email: 'bo@example.com', role: 'Student'}

function meBackend(me = meAs('Teacher', ['c1']), users: unknown[] = [teacher, student]) {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: me}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devOps]}
    }
    if (path === '/api/courses/c1/modules') {
      return {status: 200, body: [{id: 'm1', courseId: 'c1', title: 'Vecka 1', description: '', position: 1, content: []}]}
    }
    if (path === '/api/courses/c1/content') {
      return {status: 200, body: []}
    }
    if (path === '/api/users') {
      return {status: 200, body: users}
    }
    return {status: 404}
  })
}

beforeEach(() => {
  meBackend()
})

afterEach(() => {
  signOutForTests()
})

describe('AppLayout', () => {
  it('sidomenyn visar kurskoden när två kurser har samma titel', async () => {
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Teacher', ['c1', 'c9'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [devOps, {...devOps, id: 'c9', code: 'NET26-DEVOPS'}, {...devOps, id: 'c5', code: 'JAVA', title: 'Java'}]}
      }
      return {status: 404}
    })

    const page = await render('/')

    const sidebar = page.find('[data-slot="sidebar"]').text()
    expect(sidebar).toContain('DevOps · DEVOPS')
    expect(sidebar).toContain('DevOps · NET26-DEVOPS')
    expect(sidebar).toContain('Java')
    expect(sidebar).not.toContain('Java ·')
  })

  it('sidomenyn listar pågående före kommande kurser och viker undan avslutade', async () => {
    const day = (offset: number) => {
      const date = new Date()
      date.setDate(date.getDate() + offset)
      return date.toLocaleDateString('sv-SE')
    }
    const course = (id: string, title: string, startsOn: string | null, endsOn: string | null) =>
      ({...devOps, id, code: id.toUpperCase(), title, startsOn, endsOn})
    mockApi((_method, path) => {
      if (path === '/api/me') {
        return {status: 200, body: meAs('Student', ['c1'])}
      }
      if (path === '/api/courses') {
        return {status: 200, body: [
          course('ai', 'AI', day(30), day(60)),
          course('old', 'Gammal', day(-90), day(-30)),
          course('cs', 'C#', day(-10), day(10)),
          course('lia', 'LIA', day(10), day(40)),
          course('free', 'Utan datum', null, null),
        ]}
      }
      return {status: 404}
    })

    const page = await render('/')

    const menu = page.find('[data-slot="sidebar-content"]').text()
    expect(menu.indexOf('C#')).toBeLessThan(menu.indexOf('Utan datum'))
    expect(menu.indexOf('Utan datum')).toBeLessThan(menu.indexOf('LIA'))
    expect(menu.indexOf('LIA')).toBeLessThan(menu.indexOf('AI'))
    expect(menu).toContain('Avslutade')
    // Gruppen är hopfälld från start, så den avslutade kursen syns inte.
    expect(menu).not.toContain('Gammal')

    await page.find('[data-test="finished-courses"] button').trigger('click')

    expect(page.find('[data-test="finished-courses"]').text()).toContain('Gammal')
  })

  it('sidomenyn visar ingen Avslutade-grupp när alla kurser pågår', async () => {
    const page = await render('/')

    expect(page.find('[data-slot="sidebar"]').text()).not.toContain('Avslutade')
  })

  it('sidomenyn visar huvudmenyn och kurserna', async () => {
    const page = await render('/')

    const sidebar = page.find('[data-slot="sidebar"]').text()
    for (const label of ['Idag', 'Kalender', 'Inkorg', 'Att bedöma', 'Inställningar', 'Mina kurser', 'DevOps']) {
      expect(sidebar).toContain(label)
    }
  })

  it('markerar sidan man är på i sidomenyn', async () => {
    const page = await render('/calendar')

    const active = page.findAll('[data-sidebar="menu-button"][data-active="true"]')

    expect(active.map((button) => button.text())).toEqual(['Kalender'])
  })

  it('knappen i headern fäller ihop sidomenyn', async () => {
    const page = await render('/')
    const sidebar = () => page.find('[data-slot="sidebar"][data-state]')
    expect(sidebar().attributes('data-state')).toBe('expanded')

    await page.find('[data-slot="sidebar-trigger"]').trigger('click')

    expect(sidebar().attributes('data-state')).toBe('collapsed')
  })

  it('brödsmulorna följer sidan', async () => {
    const page = await render('/courses/c1/modules/1/new')

    const breadcrumbs = page.findAll('[data-slot="breadcrumb"] li:not([role="presentation"])')

    expect(breadcrumbs.map((crumb) => crumb.text())).toEqual(['Kurser', 'DevOps', 'Innehåll', 'Ny del'])
  })

  it('kurssidan visar kursen och markerar fliken man är på', async () => {
    const page = await render('/courses/c1/news')

    expect(page.find('h1').text()).toBe('DevOps')
    expect(page.find('[data-test="tab-course-news"]').attributes('aria-current')).toBe('page')
    expect(page.find('[data-test="tab-course-content"]').attributes('aria-current')).toBeUndefined()
  })

  it('en kurs som inte finns visar sidan som inte finns', async () => {
    const page = await render('/courses/finns-inte/content')

    expect(page.find('[data-test="not-found"]').exists()).toBe(true)
  })

  it('inkorgen fyller ytan under headern utan sidopadding', async () => {
    const page = await render('/inbox')

    expect(page.find('[data-test="main-full-bleed"]').exists()).toBe(true)
    expect(page.find('[data-test="main-padded"]').exists()).toBe(false)
  })

  it('andra sidor har kvar den centrerade innehållsytan', async () => {
    const page = await render('/calendar')

    expect(page.find('[data-test="main-full-bleed"]').exists()).toBe(false)
    expect(page.find('[data-test="main-padded"]').exists()).toBe(true)
  })

  it('sidans knappar hamnar i headern', async () => {
    const PageWithActions = defineComponent({
      render: () => h(PageActions, () => h('button', {'data-test': 'publish'}, 'Publicera')),
    })
    const router = createAppRouter(createMemoryHistory())
    router.addRoute('app', {path: 'page-with-actions', component: PageWithActions})

    const page = await render('/page-with-actions', router)

    expect(page.find('header #page-actions [data-test="publish"]').exists()).toBe(true)
  })

  it('hämtar /api/me och visar namn och svensk roll i sidomenyn', async () => {
    const page = await render('/')

    expect(requests.map((request) => request.path)).toContain('/api/me')
    const footer = page.find('[data-test="user-menu"]')
    expect(footer.text()).toContain('Anna Lärare')
    expect(footer.text()).toContain('Lärare · Edugrade')
  })

  it('visar Admin för en administratör', async () => {
    meBackend(meAs('Admin', ['c1'], 'Ada Admin'))

    const page = await render('/')

    expect(page.find('[data-test="user-menu"]').text()).toContain('Admin · Edugrade')
  })

  it('visar inte Att bedöma för en elev', async () => {
    meBackend(meAs('Student', ['c1'], 'Bo Elev'))

    const page = await render('/')

    expect(page.find('[data-test="user-menu"]').text()).toContain('Bo Elev')
    expect(page.find('[data-slot="sidebar"]').text()).toContain('Inkorg')
    expect(page.find('[data-slot="sidebar"]').text()).not.toContain('Att bedöma')
  })

  it('användarmenyn har Logga ut', async () => {
    const page = await render('/')
    await page.find('[data-test="user-menu"]').trigger('click')
    await flushPromises()

    expect(document.querySelector('[data-test="sign-out"]')).not.toBeNull()
  })

  it('Logga ut loggar ut och går till inloggningen', async () => {
    const {wrapper, router} = await renderApp('/')
    await wrapper.find('[data-test="user-menu"]').trigger('click')
    await flushPromises()

    ;(document.querySelector('[data-test="sign-out"]') as HTMLElement).click()
    await flushPromises()

    expect(auth.isSignedIn()).toBe(false)
    expect(router.currentRoute.value.name).toBe('login')
  })
})
