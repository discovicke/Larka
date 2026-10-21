import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'

import App from './App.vue'

describe('App', () => {
  it('renderar rubriken', () => {
    const wrapper = mount(App)

    expect(wrapper.find('h1').text()).toBe('Welcome to your new project')
  })
})
