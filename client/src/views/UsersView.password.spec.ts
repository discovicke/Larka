import {flushPromises} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import {mockApi, requests, sent} from '@/test/mockApi'
import {meAs} from '@/test/me'
import {renderApp} from '@/test/renderApp'

// Sätt lösenord i menyn på Användare (#509). En egen fil, så att den inte krockar med ändringar i UsersView.spec.ts.
const me = {id: 'u-me', schoolId: 's1', name: 'Anna Admin', email: 'anna@example.com', role: 'Admin'}
const nils = {id: 'u1', schoolId: 's1', name: 'Nils Utanlösen', email: 'nils@example.com', role: 'Student'}
const elin = {
  id: 'u2', schoolId: 's1', name: 'Elin Elev', email: 'elin@example.com', role: 'Student', microsoftLinked: true,
}

function backend(passwordStatus = 200) {
  mockApi((method, path) => {
    if (path === '/api/me') {
      return {status: 200, body: meAs('Admin')}
    }
    if (method === 'GET' && path === '/api/users') {
      return {status: 200, body: [me, nils, elin]}
    }
    if (method === 'POST' && path === '/api/users/u1/password') {
      return passwordStatus === 200
        ? {status: 200, body: nils}
        : {status: passwordStatus, body: 'the user signs in with Microsoft'}
    }
    return {status: 404}
  })
}

function dialog(): HTMLElement {
  return document.querySelector('[role="dialog"]') as HTMLElement
}

async function click(element: Element | null) {
  ;(element as HTMLElement).click()
  await flushPromises()
}

async function openMenu(wrapper: Awaited<ReturnType<typeof renderApp>>['wrapper'], name: string) {
  await wrapper.find(`[aria-label="Åtgärder för ${name}"]`).trigger('keydown', {key: 'Enter'})
  await flushPromises()
}

async function type(value: string) {
  const input = dialog().querySelector('[data-test="set-password"]') as HTMLInputElement
  input.value = value
  input.dispatchEvent(new Event('input'))
  await flushPromises()
}

describe('UsersView, Sätt lösenord', () => {
  it('sätter ett tillfälligt lösenord och visar en toast', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await openMenu(wrapper, 'Nils Utanlösen')
    await click(document.querySelector('[data-test="row-set-password"]'))
    expect(dialog().textContent).toContain('Sätt lösenord för Nils Utanlösen')

    await type('hemligt123')
    await click(dialog().querySelector('[data-test="save-password"]'))

    expect(sent('POST', '/api/users/u1/password')).toEqual({password: 'hemligt123'})
    expect(wrapper.find('[data-test="toast"]').text()).toContain('Lösenordet är sparat. Ge det till Nils Utanlösen.')
    expect(document.querySelector('[role="dialog"]')).toBeNull()
  })

  it('kräver minst 8 tecken innan något skickas', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await openMenu(wrapper, 'Nils Utanlösen')
    await click(document.querySelector('[data-test="row-set-password"]'))
    await type('kort')
    await click(dialog().querySelector('[data-test="save-password"]'))

    expect(dialog().querySelector('[data-test="set-password-error"]')?.textContent)
      .toContain('Lösenordet måste vara minst 8 tecken.')
    expect(requests.some((request) => request.method === 'POST')).toBe(false)
  })

  it('visar serverns svar på svenska', async () => {
    backend(409)
    const {wrapper} = await renderApp('/users')

    await openMenu(wrapper, 'Nils Utanlösen')
    await click(document.querySelector('[data-test="row-set-password"]'))
    await type('hemligt123')
    await click(dialog().querySelector('[data-test="save-password"]'))

    expect(dialog().querySelector('[data-test="set-password-server-error"]')?.textContent)
      .toContain('Hen loggar in med Microsoft och behöver inget lösenord i Lärka.')
  })

  it('finns inte för den som loggar in med Microsoft', async () => {
    backend()
    const {wrapper} = await renderApp('/users')

    await openMenu(wrapper, 'Elin Elev')

    expect(document.querySelector('[data-test="row-set-password"]')).toBeNull()
  })
})
