import {flushPromises, mount} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'
import {defineComponent, h} from 'vue'
import {createMemoryHistory, type Router} from 'vue-router'

import App from '@/App.vue'
import PageActions from '@/components/layout/PageActions.vue'
import {createAppRouter} from '@/router'

let wrapper: ReturnType<typeof mount> | undefined

async function render(path: string, router: Router = createAppRouter(createMemoryHistory())) {
  await router.push(path)
  // Fäst i dokumentet så att <PageActions> (Teleport) hittar headern.
  wrapper = mount(App, {global: {plugins: [router]}, attachTo: document.body})
  await flushPromises()
  return wrapper
}

afterEach(() => {
  wrapper?.unmount()
  wrapper = undefined
})

describe('AppLayout', () => {
  it('sidomenyn visar huvudmenyn och kurserna', async () => {
    const page = await render('/')

    const sidebar = page.find('[data-slot="sidebar"]').text()
    for (const label of ['Idag', 'Kalender', 'Inkorg', 'Att bedöma', 'Mina kurser', 'DevOps']) {
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
    const page = await render('/courses/devops/modules/1/new')

    const breadcrumbs = page.findAll('[data-slot="breadcrumb"] li:not([role="presentation"])')

    expect(breadcrumbs.map((crumb) => crumb.text())).toEqual(['Kurser', 'DevOps', 'Innehåll', 'Ny del'])
  })

  it('kurssidan visar kursen och markerar fliken man är på', async () => {
    const page = await render('/courses/devops/news')

    expect(page.find('h1').text()).toBe('DevOps')
    expect(page.find('[data-test="tab-course-news"]').attributes('aria-current')).toBe('page')
    expect(page.find('[data-test="tab-course-content"]').attributes('aria-current')).toBeUndefined()
  })

  it('en kurs som inte finns visar sidan som inte finns', async () => {
    const page = await render('/courses/finns-inte/content')

    expect(page.find('[data-test="not-found"]').exists()).toBe(true)
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
})
