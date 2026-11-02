import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'

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

  it('visar tydligt fel när repot inte kunde skapas', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: null,
        repoProvisioningState: 'Failed',
        repoProvisioningError: 'Mallrepot är inte markerat som template.',
      },
    })

    expect(wrapper.find('[data-test="github-provisioning-failed"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Mallrepot är inte markerat som template.')
    expect(wrapper.find('[data-test="student-repo-provisioning-failed"]').exists()).toBe(true)
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

  it('visar kravstatus från check runs', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        requirements: [
          {
            id: 'r1',
            title: 'Bygger',
            testPattern: 'Build.*',
            isManual: false,
            position: 0,
          },
          {
            id: 'r2',
            title: 'Reflektion',
            testPattern: null,
            isManual: true,
            position: 1,
          },
        ],
        checkRuns: [
          {id: 'c1', name: 'Build.test', status: 'completed', conclusion: 'success'},
        ],
      },
    })

    expect(wrapper.find('[data-test="student-requirements"]').text()).toContain('Bygger')
    expect(wrapper.find('[data-test="student-requirements"]').text()).toContain('Godkänd')
    expect(wrapper.find('[data-test="student-requirements"]').text()).toContain('Bedöms av lärare')
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

  it('matchar matrix-jobb mot jobbnamnet', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        requirements: [
          {
            id: 'r1',
            title: 'Bygger',
            testPattern: 'Build and test',
            isManual: false,
            position: 0,
          },
        ],
        checkRuns: [
          {id: 'c1', name: 'Build and test (ubuntu-latest)', status: 'completed', conclusion: 'success'},
        ],
      },
    })

    expect(wrapper.find('[data-test="student-requirements"]').text()).toContain('Godkänd')
  })

  it('säger att ingen check matchar när det finns check runs men mönstret inte träffar', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        requirements: [
          {
            id: 'r1',
            title: 'Bygger',
            testPattern: 'Workflow name',
            isManual: false,
            position: 0,
          },
        ],
        checkRuns: [
          {id: 'c1', name: 'build', status: 'completed', conclusion: 'success'},
        ],
      },
    })

    expect(wrapper.find('[data-test="student-requirements"]').text()).toContain('Ingen matchande check')
  })

  it('kopierar klonkommandot', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {
      value: {writeText},
      configurable: true,
    })
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
      },
    })

    await wrapper.find('[data-test="student-clone-copy"]').trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith(
      'git clone https://github.com/skol-org/kalle-labb-1.git',
    )
    expect(wrapper.find('[data-test="student-clone-copy"]').text()).toContain('Kopierat')
  })

  it('visar gruppens namn och medlemmar', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/grupp-1',
        groupName: 'Grupp 1',
        groupMembers: ['Kalle', 'Nora'],
      },
    })

    expect(wrapper.find('[data-test="student-group"]').text()).toContain('Grupp 1')
    expect(wrapper.find('[data-test="student-group"]').text()).toContain('Kalle, Nora')
  })

  it('visar låst repo och inlämnad commit utan åtgärder', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        lockedAt: '2026-11-01T12:00:00Z',
        submissionSha: 'abc1234567890',
      },
    })

    expect(wrapper.find('[data-test="student-repo-locked-badge"]').text()).toContain('Repot är låst')
    expect(wrapper.find('[data-test="student-repo-locked"]').text()).toContain('abc1234')
    expect(wrapper.find('[data-test="student-repo-locked"]').text()).toContain('går inte att ändra')
  })

  it('visar varför ett låst repo saknar inlämning', () => {
    const wrapper = mount(GitHubAssignmentBlock, {
      props: {
        hasRepoMode: true,
        gitHubLogin: 'elev-kalle',
        repoUrl: 'https://github.com/skol-org/kalle-labb-1',
        lockedAt: '2026-11-01T12:00:00Z',
        lockError: 'Ingen commit före deadline.',
      },
    })

    expect(wrapper.find('[data-test="student-repo-locked"]').text()).toContain('Ingen commit före deadline.')
  })
})
