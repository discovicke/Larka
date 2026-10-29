import {afterEach, describe, expect, it} from 'vitest'

import {signOutForTests} from '@/test/auth'
import {mockApi} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

const devOps = {id: 'c1', schoolId: 's1', code: 'DEVOPS', title: 'DevOps'}

afterEach(() => {
  signOutForTests()
})

function backend() {
  mockApi((_method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Teacher', ['c1'])}
    }
    if (path === '/api/courses') {
      return {status: 200, body: [devOps]}
    }
    return {status: 404}
  })
}

function crumbs(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper']) {
  return wrapper.get('header nav').findAll('li').filter((item) => item.attributes('role') !== 'presentation')
}

describe('AppHeader', () => {
  it('mellanleden är länkar och sista smulan är sidan själv', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/news')

    const items = crumbs(wrapper)
    expect(items.map((item) => item.text())).toEqual(['Kurser', 'DevOps', 'Nyheter'])
    expect(items[0].get('a').attributes('href')).toBe('/groups?filter=courses')
    expect(items[1].get('a').attributes('href')).toBe('/courses/c1/content')
    expect(items[2].find('a').exists()).toBe(false)
    expect(items[2].get('[aria-current="page"]').text()).toBe('Nyheter')
  })

  it('under sm döljs alla utom de två sista smulorna', async () => {
    backend()
    const {wrapper} = await renderApp('/courses/c1/assignments/l1/submissions/s1')

    const hidden = crumbs(wrapper).map((item) => item.classes().includes('max-sm:hidden'))
    expect(hidden).toEqual([true, true, true, false, false])
  })
})
