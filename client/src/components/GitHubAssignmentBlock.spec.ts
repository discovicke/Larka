import {mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'

import GitHubAssignmentBlock from './GitHubAssignmentBlock.vue'

describe('GitHubAssignmentBlock', () => {
  it('visar ingenting när uppgiften inte använder GitHub (hasRepoMode = false)', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: false,
      },
    })

    expect(wrapper.find('[data-test="github-assignment-block"]').exists()).toBe(false)
  })

  it('visar CTA-knapp för att koppla GitHub om eleven saknar koppling', async () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: null,
      },
    })

    expect(wrapper.find('[data-test="github-unlinked"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="github-connect-cta"]').exists()).toBe(true)

    await wrapper.find('[data-test="github-connect-cta"]').trigger('click')
    expect(wrapper.emitted('connect')).toBeTruthy()
  })

  it('visar vänteläge när eleven har kopplat GitHub men repot inte skapats än', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: null,
      },
    })

    expect(wrapper.find('[data-test="github-pending-repo"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('@elev-kalle')
    expect(wrapper.find('[data-test="student-repo-link"]').exists()).toBe(false)
  })

  it('visar repolänk, senaste commit och status när repot är klart', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        latestCommit: 'abc1234567890',
        checkStatus: 'Green',
      },
    })

    expect(wrapper.find('[data-test="github-repo-ready"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="student-repo-link"]').attributes('href')).toBe(
      'https://github.com/skol-org/kalle-labb-1',
    )
    expect(wrapper.find('[data-test="student-repo-commit"]').text()).toContain('abc1234')
    expect(wrapper.find('[data-test="student-repo-status"]').text()).toContain('Tester godkända')
  })

  it('visar underkänd status när checkStatus är Red', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        checkStatus: 'Red',
      },
    })

    expect(wrapper.find('[data-test="student-repo-status"]').text()).toContain('Tester underkända')
  })
})
