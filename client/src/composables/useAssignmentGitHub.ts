import {computed, ref, watch} from 'vue'

import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api} from '@/lib/api'
import {
  defaultGroupRepoNamePattern,
  defaultRepoNamePattern,
  usesGroupRepo,
  usesRepo,
  type ContentForm,
} from '@/lib/createContent'
import {fetchCourseStudents} from '@/lib/coursesApi'
import {
  fetchGitHubTemplateCommit,
  fetchGitHubTemplateTests,
  fetchGitHubTemplates,
  fetchUnlinkedGitHubStudents,
} from '@/lib/integrations'

/** All GitHub-data som hör till uppgiftsformuläret. Formulärvyn äger bara fälten och sparandet. */
export function useAssignmentGitHub(form: ContentForm, courseId: string) {
  const {me} = useCurrentUser()
  const githubEnabled = computed(() => me.value?.gitHubEnabled === true)

  const templates = ref<Awaited<ReturnType<typeof fetchGitHubTemplates>>>([])
  const templateCommit = ref<Awaited<ReturnType<typeof fetchGitHubTemplateCommit>> | null>(null)
  const requirements = ref<{loading: boolean, error: string}>({loading: false, error: ''})
  const repoNamePreview = ref<{name: string, pattern: string} | null>(null)
  const unlinkedStudents = ref<Awaited<ReturnType<typeof fetchUnlinkedGitHubStudents>>>([])
  const showUnlinkedStudents = ref(false)
  const courseStudents = ref<Awaited<ReturnType<typeof fetchCourseStudents>>>([])

  const templateCommitLoader = useAsync(async (fullName: string) => {
    templateCommit.value = await fetchGitHubTemplateCommit(fullName)
  })
  const templatesLoader = useAsync(async () => {
    templates.value = await fetchGitHubTemplates()
    if (form.templateRepo) {
      await loadTemplateCommit(form.templateRepo)
    }
  })
  const previewLoader = useAsync(async () => {
    const {data} = await api.POST('/api/content/github/repo-name-preview', {
      body: {
        pattern: form.repoNamePattern.trim(),
        assignmentTitle: form.title.trim() || 'Uppgift',
        gitHubLogin: me.value?.gitHubLogin ?? 'elev',
        studentName: me.value?.name ?? 'Elev',
        workMode: form.workMode,
        groupName: form.workMode === 'Group'
          ? 'grupp-1'
          : null,
        groupNumber: form.workMode === 'Group'
          ? 1
          : null,
      },
    })
    repoNamePreview.value = data ?? null
  })
  const unlinkedLoader = useAsync(async () => {
    unlinkedStudents.value = await fetchUnlinkedGitHubStudents(courseId)
  })
  const courseStudentsLoader = useAsync(async () => {
    courseStudents.value = await fetchCourseStudents(courseId)
  })

  const templateOptions = computed(() => {
    const options = templates.value.map((template) => ({value: template.fullName, label: template.fullName}))
    if (form.templateRepo && !options.some((option) => option.value === form.templateRepo)) {
      options.unshift({value: form.templateRepo, label: `${form.templateRepo} (inte markerat som template)`})
    }
    return options
  })
  const selectedTemplate = computed(() =>
    templates.value.find((template) => template.fullName === form.templateRepo) ?? null)
  const templateNotMarked = computed(() =>
    Boolean(form.templateRepo)
    && !templatesLoader.loading.value
    && !templatesLoader.error.value
    && templates.value.length > 0
    && selectedTemplate.value === null)

  async function loadTemplates(force = false) {
    if (!githubEnabled.value || (!force && templates.value.length > 0)) {
      return
    }
    await templatesLoader.run()
  }

  async function loadTemplateCommit(fullName: string) {
    templateCommit.value = null
    if (!fullName.includes('/')) {
      return
    }
    await templateCommitLoader.run(fullName)
  }

  async function loadRepoNamePreview() {
    if (!form.repoNamePattern.trim()) {
      repoNamePreview.value = null
      return
    }
    await previewLoader.run()
  }

  async function loadUnlinkedStudents() {
    if (!githubEnabled.value) {
      unlinkedStudents.value = []
      return
    }
    await unlinkedLoader.run()
  }

  async function loadCourseStudents() {
    if (!githubEnabled.value || !usesGroupRepo(form)) {
      courseStudents.value = []
      return
    }
    await courseStudentsLoader.run()
  }

  async function loadRequirementsFromTemplate() {
    requirements.value = {loading: true, error: ''}
    try {
      if (!form.templateRepo) {
        requirements.value = {loading: false, error: 'Välj ett mallrepo först.'}
        return
      }
      const names = await fetchGitHubTemplateTests(form.templateRepo)
      form.requirements = names.map((name) => ({title: name, testPattern: name, isManual: false}))
      requirements.value = {
        loading: false,
        error: names.length === 0
          ? 'Hittade inga jobbnamn i mallen.'
          : '',
      }
    } catch (exception) {
      requirements.value = {loading: false, error: exception instanceof Error
        ? exception.message
        : 'Kunde inte läsa in krav.'}
    }
  }

  function addRequirement() {
    form.requirements.push({title: '', testPattern: '', isManual: false})
  }

  function removeRequirement(index: number) {
    form.requirements.splice(index, 1)
  }

  async function loadAll() {
    if (!usesRepo(form)) {
      return
    }
    await loadTemplates()
    await loadTemplateCommit(form.templateRepo)
    await loadRepoNamePreview()
    await loadUnlinkedStudents()
    if (usesGroupRepo(form)) {
      await loadCourseStudents()
    }
  }

  watch(() => form.templateRepo, (fullName) => {
    void loadTemplateCommit(fullName)
  })
  watch([() => form.repoNamePattern, () => form.title, () => form.workMode], () => {
    if (usesRepo(form)) {
      void loadRepoNamePreview()
    }
  })
  // Samma växling laddar både mallrepon och elevlistan, så det behövs bara en watcher.
  watch(() => usesRepo(form), (repoMode) => {
    if (repoMode) {
      void loadTemplates()
      void loadUnlinkedStudents()
    }
  })
  watch(() => form.workMode, (mode, previous) => {
    const pattern = form.repoNamePattern.trim()
    if (pattern === defaultRepoNamePattern && mode === 'Group') {
      form.repoNamePattern = defaultGroupRepoNamePattern
    } else if (pattern === defaultGroupRepoNamePattern && mode === 'Individual' && previous === 'Group') {
      form.repoNamePattern = defaultRepoNamePattern
    }
    if (mode === 'Group') {
      void loadCourseStudents()
    }
  })

  return {
    githubEnabled,
    templates,
    templatesLoading: templatesLoader.loading,
    templatesError: templatesLoader.error,
    templateCommit,
    templateCommitLoading: templateCommitLoader.loading,
    templateCommitError: templateCommitLoader.error,
    templateOptions,
    selectedTemplate,
    templateNotMarked,
    requirementsLoading: computed(() => requirements.value.loading),
    requirementsError: computed(() => requirements.value.error),
    repoNamePreview,
    repoNamePreviewLoading: previewLoader.loading,
    repoNamePreviewError: previewLoader.error,
    unlinkedStudents,
    unlinkedStudentsLoading: unlinkedLoader.loading,
    unlinkedStudentsError: unlinkedLoader.error,
    showUnlinkedStudents,
    courseStudents,
    courseStudentsLoading: courseStudentsLoader.loading,
    courseStudentsError: courseStudentsLoader.error,
    loadTemplates,
    loadRequirementsFromTemplate,
    loadAll,
    addRequirement,
    removeRequirement,
  }
}
