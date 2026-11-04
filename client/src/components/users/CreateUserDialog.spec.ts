import {flushPromises, mount, type VueWrapper} from '@vue/test-utils'
import {afterEach, describe, expect, it} from 'vitest'

import CreateUserDialog from '@/components/users/CreateUserDialog.vue'
import {type Schema} from '@/lib/api'
import {mockApi, requests, sent} from '@/test/mockApi'

// Dialogen portas till body, så den måste tas bort mellan testerna.
const mounted: VueWrapper[] = []

afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount())
})

async function openDialog(
  {roles, createStatus = 201, microsoft = false}: {roles?: Schema<'UserRole'>[], createStatus?: number, microsoft?: boolean} = {},
) {
  mockApi((method, path, body) => {
    if (path === '/api/auth/providers') {
      return {status: 200, body: {microsoft: microsoft
        ? {clientId: 'c', tenantId: 't'}
        : null, schoolName: 'Edugrade'}}
    }
    if (method === 'POST' && path === '/api/users') {
      return createStatus === 201
        ? {status: 201, body: {id: 'u9', ...(body as object)}}
        : {status: createStatus, body: 'email is already in use'}
    }
    return {status: 404}
  })
  const wrapper = mount(CreateUserDialog, {
    props: {open: false, schoolId: 's1', ...(roles
      ? {roles}
      : {})},
    attachTo: document.body,
  })
  await wrapper.setProps({open: true})
  await flushPromises()
  mounted.push(wrapper)
  return wrapper
}

const field = <T extends HTMLElement = HTMLInputElement>(name: string) =>
  document.querySelector(`[data-test="${name}"]`) as T | null

async function fill(values: {name?: string, email?: string, password?: string, role?: string}) {
  for (const [name, key] of [['new-user-name', 'name'], ['new-user-email', 'email'], ['new-user-password', 'password']] as const) {
    const value = values[key]
    if (value !== undefined) {
      field(name)!.value = value
      field(name)!.dispatchEvent(new Event('input'))
    }
  }
  if (values.role) {
    const select = field<HTMLSelectElement>('new-user-role')!
    select.value = values.role
    select.dispatchEvent(new Event('change'))
  }
  await flushPromises()
}

async function submit() {
  field<HTMLButtonElement>('confirm-create-user')!.click()
  await flushPromises()
}

describe('CreateUserDialog', () => {
  it('erbjuder alla skolroller, med Elev förvald', async () => {
    await openDialog()

    const select = field<HTMLSelectElement>('new-user-role')!
    expect([...select.options].map((option) => option.textContent?.trim())).toEqual(['Elev', 'Lärare', 'Admin'])
    expect(select.value).toBe('Student')
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ny användare')
  })

  it('kräver namn och giltig e-post innan något skickas', async () => {
    await openDialog()

    await submit()

    expect(field('new-user-name-error')?.textContent).toBe('Ange ett namn.')
    expect(field('new-user-email-error')?.textContent).toBe('Ange en giltig e-postadress.')
    expect(requests.some((request) => request.method === 'POST')).toBe(false)
  })

  it('skapar en admin i skolan, meddelar den nya användaren och stänger', async () => {
    const wrapper = await openDialog()

    await fill({name: ' Ada Admin ', email: 'ada@example.com', password: 'hemligt123', role: 'Admin'})
    await submit()

    expect(sent('POST', '/api/users')).toEqual({
      schoolId: 's1', name: 'Ada Admin', email: 'ada@example.com', role: 'Admin', password: 'hemligt123',
    })
    expect(wrapper.emitted('created')?.[0]).toEqual([expect.objectContaining({id: 'u9', name: 'Ada Admin'})])
    expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
  })

  it('visar serverns fel och stannar öppen', async () => {
    const wrapper = await openDialog({createStatus: 400})

    await fill({name: 'Ny Elev', email: 'siv@example.com'})
    await submit()

    expect(document.querySelector('[role="alert"]')?.textContent)
      .toContain('E-postadressen används redan av en annan användare.')
    expect(wrapper.emitted('created')).toBeUndefined()
  })

  it('med bara rollen Lärare visas inget rollval och läraren skapas som lärare', async () => {
    await openDialog({roles: ['Teacher']})

    expect(field('new-user-role')).toBeNull()
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Ny lärare')

    await fill({name: 'Tove Ny', email: 'tove@example.com'})
    await submit()

    expect(sent('POST', '/api/users')).toMatchObject({role: 'Teacher', password: null})
  })

  it('med Microsoft-inloggning visas inget lösenordsfält', async () => {
    await openDialog({microsoft: true})

    expect(field('new-user-password')).toBeNull()
    expect(field('new-user-microsoft')).not.toBeNull()
  })

  it('tömmer formuläret när dialogen öppnas igen', async () => {
    const wrapper = await openDialog()
    await fill({name: 'Halvfärdig', email: 'x'})
    await submit()

    await wrapper.setProps({open: false})
    await wrapper.setProps({open: true})
    await flushPromises()

    expect(field('new-user-name')!.value).toBe('')
    expect(field('new-user-name-error')).toBeNull()
  })
})
