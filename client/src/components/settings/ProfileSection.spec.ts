import {flushPromises, mount} from '@vue/test-utils'
import {beforeEach, describe, expect, it} from 'vitest'

import {useCurrentUser} from '@/composables/useCurrentUser'
import {AVATAR_MAX_BYTES, avatarProblem} from '@/lib/avatar'
import type {Me} from '@/lib/me'
import {mockApi, requests} from '@/test/mockApi'
import {meAs} from '@/test/me'
import ProfileSection from './ProfileSection.vue'

const withAvatar: Me = {...meAs('Student', [], 'Anna Elev'), avatarUrl: '/api/avatars/u-me/ny'}

function backend(status = 200) {
  mockApi((method, path) => {
    if (method === 'POST' && path === '/api/me/avatar') {
      return status === 200
        ? {status, body: withAvatar}
        : {status, body: 'a profile picture must be a png, jpg, gif or webp image'}
    }
    if (method === 'DELETE' && path === '/api/me/avatar') {
      return {status: 200, body: {...withAvatar, avatarUrl: null}}
    }
    return {status: 404}
  })
}

async function choose(wrapper: ReturnType<typeof mount>, file: File) {
  const input = wrapper.find('[data-test="avatar-input"]')
  Object.defineProperty(input.element, 'files', {value: [file], configurable: true})
  await input.trigger('change')
  await flushPromises()
}

function image(name: string, size = 10): File {
  return new File([new Uint8Array(size)], name, {type: 'image/png'})
}

describe('ProfileSection', () => {
  beforeEach(() => {
    const {me, resetCurrentUser} = useCurrentUser()
    resetCurrentUser()
    me.value = meAs('Student', [], 'Anna Elev')
  })

  it('laddar upp en vald bild och visar knapparna för att byta och ta bort den', async () => {
    backend()
    const wrapper = mount(ProfileSection)
    expect(wrapper.find('[data-test="avatar-upload"]').text()).toBe('Ladda upp bild')
    expect(wrapper.find('[data-test="avatar-remove"]').exists()).toBe(false)

    await choose(wrapper, image('jag.png'))

    expect(requests.some((request) => request.method === 'POST' && request.path === '/api/me/avatar')).toBe(true)
    expect(useCurrentUser().me.value?.avatarUrl).toBe('/api/avatars/u-me/ny')
    expect(wrapper.find('[data-test="avatar-upload"]').text()).toBe('Byt bild')
    expect(wrapper.find('[data-test="avatar-remove"]').exists()).toBe(true)
  })

  it('tar bort bilden', async () => {
    backend()
    useCurrentUser().me.value = withAvatar
    const wrapper = mount(ProfileSection)

    await wrapper.find('[data-test="avatar-remove"]').trigger('click')
    await flushPromises()

    expect(requests.some((request) => request.method === 'DELETE' && request.path === '/api/me/avatar')).toBe(true)
    expect(useCurrentUser().me.value?.avatarUrl).toBeNull()
    expect(wrapper.find('[data-test="avatar-remove"]').exists()).toBe(false)
  })

  it('skickar inte en fil som inte är en bild, och säger varför', async () => {
    backend()
    const wrapper = mount(ProfileSection)

    await choose(wrapper, new File(['%PDF'], 'cv.pdf', {type: 'application/pdf'}))

    expect(requests).toHaveLength(0)
    expect(wrapper.find('[data-test="avatar-error"]').text()).toContain('PNG, JPG, GIF eller WebP')
  })

  it('visar serverns fel på svenska när den inte tar emot bilden', async () => {
    backend(400)
    const wrapper = mount(ProfileSection)

    await choose(wrapper, image('jag.png'))

    expect(wrapper.find('[data-test="avatar-error"]').text()).toBe('Välj en bild i formatet PNG, JPG, GIF eller WebP.')
    expect(useCurrentUser().me.value?.avatarUrl).toBeUndefined()
  })
})

describe('avatarProblem', () => {
  it('godtar vanliga bildtyper oavsett skiftläge', () => {
    expect(avatarProblem(image('a.PNG'))).toBeNull()
    expect(avatarProblem(image('a.jpeg'))).toBeNull()
    expect(avatarProblem(image('a.webp'))).toBeNull()
  })

  it('nekar tomma och för stora bilder', () => {
    expect(avatarProblem(image('a.png', 0))).toBe('Bilden är tom.')
    expect(avatarProblem(image('a.png', AVATAR_MAX_BYTES + 1))).toBe('Bilden får vara högst 5 MB.')
  })
})
