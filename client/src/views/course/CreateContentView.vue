<script setup lang="ts">
import {
  PhChalkboardTeacher,
  PhFiles,
  PhGithubLogo,
  PhPlus,
  PhTextAa,
  PhTrash,
  PhWarning,
} from '@phosphor-icons/vue'
import {type Component, computed, onMounted, reactive, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import AssignmentGroupEditor from '@/components/course/AssignmentGroupEditor.vue'
import AttachmentList from '@/components/course/AttachmentList.vue'
import ContentFormFields from '@/components/course/ContentFormFields.vue'
import PageActions from '@/components/layout/PageActions.vue'
import {Alert, AlertDescription, AlertTitle} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {Field, FieldError, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {Skeleton} from '@/components/ui/skeleton'
import {useAssignmentGitHub} from '@/composables/useAssignmentGitHub'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {ApiError, errorMessage, type Schema} from '@/lib/api'
import {fetchAssignmentGroups, updateAssignmentGroups} from '@/lib/assignmentGroups'
import {partTypeOf} from '@/lib/content'
import {createContentWithLink, fetchContent, updateContent, updateContentLink} from '@/lib/contentApi'
import {
  defaultGroupRepoNamePattern,
  defaultRepoNamePattern,
  emptyForm,
  publishSteps,
  toRequests,
  toUpdateRequests,
  usesGroupRepo,
  usesRepo,
  validate,
} from '@/lib/createContent'
import {toDateTimeLocal} from '@/lib/form'
import {fetchCourseModules} from '@/lib/modulesApi'
import NotFoundView from '@/views/NotFoundView.vue'

const submissionModes: {value: Schema<'SubmissionMode'>, label: string, description: string, icon: Component}[] = [
  {value: 'File', label: 'Filer och foton', description: 'Dokument, bilder, mätprotokoll. Funkar från mobilen.', icon: PhFiles},
  {value: 'Text', label: 'Text', description: 'Eleven skriver svaret direkt i Lärka.', icon: PhTextAa},
  {value: 'OnSite', label: 'Bedöms på plats', description: 'Du bockar av moment på lektionen, ingen inlämning.', icon: PhChalkboardTeacher},
  {value: 'Repo', label: 'GitHub-repo', description: 'Varje elev får ett eget repo från din mall.', icon: PhGithubLogo},
]

const route = useRoute()
const router = useRouter()
const courseId = String(route.params.courseId)
const moduleId = String(route.params.moduleId)
const {can} = useCurrentUser()
const canManageAttachments = computed(() => can('manageAttachments', courseId))
const linkId = computed(() => route.params.linkId
  ? String(route.params.linkId)
  : null)
const isEditing = computed(() => linkId.value !== null)
const form = reactive(emptyForm())

const {
  githubEnabled,
  templates,
  templatesLoading,
  templatesError,
  templateCommit,
  templateCommitLoading,
  templateCommitError,
  templateOptions,
  selectedTemplate,
  templateNotMarked,
  requirementsLoading,
  requirementsError,
  repoNamePreview,
  repoNamePreviewLoading,
  repoNamePreviewError,
  unlinkedStudents,
  unlinkedStudentsLoading,
  unlinkedStudentsError,
  showUnlinkedStudents,
  courseStudents,
  courseStudentsLoading,
  courseStudentsError,
  loadTemplates,
  loadRequirementsFromTemplate,
  loadAll,
  addRequirement,
  removeRequirement,
} = useAssignmentGitHub(form, courseId)

const availableSubmissionModes = computed(() => submissionModes.filter((mode) =>
  mode.value !== 'Repo' || githubEnabled.value))

// Felen visas först när läraren försökt spara.
const triedToSave = ref(false)
const errors = computed(() => triedToSave.value
  ? validate(form, true, courseStudents.value.map((student) => student.id))
  : {})
const saving = ref(false)
const serverError = ref('')

const module = ref<Schema<'ModuleWithContentDto'>>()
const part = ref<Schema<'ModuleContentItem'>>()
const loading = ref(true)
const notFound = ref(false)
const loadError = ref('')

// Redigeringen började på delens sida och går tillbaka dit. En ny del visas i kursens innehåll.
const backRoute = computed(() => linkId.value
  ? {name: 'part', params: {courseId, linkId: linkId.value}}
  : {name: 'course-content', params: {courseId}})

async function load() {
  loading.value = true
  notFound.value = false
  loadError.value = ''
  try {
    const foundModule = (await fetchCourseModules(courseId)).find((candidate) => candidate.id === moduleId)
    if (!foundModule) {
      notFound.value = true
      return
    }

    module.value = foundModule
    if (!isEditing.value) {
      return
    }

    const foundPart = foundModule.content.find((candidate) => candidate.linkId === linkId.value)
    if (!foundPart || foundPart.type === 'News') {
      notFound.value = true
      return
    }

    part.value = foundPart
    const content = await fetchContent(foundPart.contentId)
    if (!content) {
      notFound.value = true
      return
    }

    form.partType = partTypeOf(foundPart)
    form.title = content.title
    form.bodyMd = content.bodyMd
    form.publishAt = toDateTimeLocal(foundPart.publishAt)
    form.dueAt = toDateTimeLocal(foundPart.dueAt)
    form.gradeScale = content.assignment?.gradeScale ?? 'PassFail'
    form.submissionModes = content.assignment?.submissionModes ?? []
    form.templateRepo = content.assignment?.templateRepo ?? ''
    form.repoNamePattern = content.assignment?.repoNamePattern ?? defaultRepoNamePattern
    form.workMode = content.assignment?.workMode ?? 'Individual'
    if (form.workMode === 'Group' && linkId.value) {
      form.groups = (await fetchAssignmentGroups(linkId.value)).map((group) => ({
        id: group.id,
        name: group.name,
        studentIds: group.members.map((member) => member.id),
      }))
    }
    form.requirements = (content.assignment?.requirements ?? []).map((requirement) => ({
      title: requirement.title,
      testPattern: requirement.testPattern ?? '',
      isManual: requirement.isManual,
    }))
    form.isPinned = foundPart.isPinned
  } catch (exception) {
    if (exception instanceof ApiError && exception.status === 404) {
      notFound.value = true
    } else {
      loadError.value = errorMessage(exception)
    }
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await load()
  await loadAll()
})

async function save(status: Schema<'ContentStatus'> = 'Published') {
  triedToSave.value = true
  if (Object.keys(validate(
    form,
    true,
    courseStudents.value.map((student) => student.id),
  )).length > 0) {
    return
  }

  if (templateNotMarked.value) {
    serverError.value = 'Mallrepot är inte markerat som template i GitHub. Markera det som Template repository och uppdatera listan.'
    return
  }

  saving.value = true
  serverError.value = ''
  try {
    if (isEditing.value) {
      const current = part.value
      if (!current) {
        return
      }

      const {content, link} = toUpdateRequests(form)
      await updateContent(current.contentId, content)
      await updateContentLink(current.contentId, current.linkId, link)
      if (usesGroupRepo(form)) {
        await updateAssignmentGroups(current.linkId, form.groups)
      }
    } else {
      const {content, link} = toRequests(form, status, {courseId, moduleId})
      await createContentWithLink({content, link})
    }

    await router.push(backRoute.value)
  } catch (exception) {
    serverError.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Skeleton v-if="loading" class="h-64 w-full"/>
  <NotFoundView v-else-if="notFound"/>
  <Alert v-else-if="loadError" variant="destructive">
    <AlertDescription class="flex flex-wrap items-center justify-between gap-3">
      <span>{{ loadError }}</span>
      <Button variant="outline" size="sm" @click="load">Försök igen</Button>
    </AlertDescription>
  </Alert>
  <div v-else-if="module" class="flex flex-wrap items-start gap-6">
    <PageActions>
      <Button variant="outline" as-child>
        <RouterLink :to="backRoute">Avbryt</RouterLink>
      </Button>
      <template v-if="isEditing">
        <Button :disabled="saving" @click="save()">Spara</Button>
      </template>
      <template v-else>
        <Button variant="outline" :disabled="saving" @click="save('Draft')">Spara utkast</Button>
        <Button :disabled="saving" @click="save('Published')">Publicera</Button>
      </template>
    </PageActions>

    <form class="flex min-w-0 flex-[999_1_560px] flex-col gap-5" @submit.prevent="save()">
      <div class="flex flex-col gap-1">
        <h1 class="text-3xl leading-tight">
          {{ isEditing
            ? 'Redigera del'
            : 'Ny del' }}
        </h1>
        <p class="text-muted-foreground">{{ module.title }}</p>
      </div>

      <Alert v-if="serverError" variant="destructive">
        <AlertDescription>{{ serverError }}</AlertDescription>
      </Alert>

      <ContentFormFields
        :form="form"
        :is-editing="isEditing"
        :errors="errors"
        :submission-modes="availableSubmissionModes"
      />

      <!-- Reponamn, arbetssätt, regler och krav kommer i följande GitHub-issues. -->
      <section v-if="usesRepo(form)" class="bg-card flex flex-col gap-4 rounded-[11px] border p-5">
        <div class="flex items-center gap-3">
          <PhGithubLogo class="size-5"/>
          <div class="flex flex-col">
            <h2 class="text-base">GitHub-inställningar</h2>
            <span class="text-muted-foreground text-[13px]">
              {{ usesGroupRepo(form)
                ? 'Varje grupp får ett gemensamt privat repo från din mall'
                : 'Varje elev får ett eget privat repo från din mall' }}
            </span>
          </div>
        </div>

        <Field>
          <FieldLabel for="repo-work-mode">Arbetssätt</FieldLabel>
          <NativeSelect
            id="repo-work-mode"
            v-model="form.workMode"
            :disabled="isEditing"
            data-test="repo-work-mode"
          >
            <NativeSelectOption value="Individual">Individuella repon</NativeSelectOption>
            <NativeSelectOption value="Group">Grupprepon</NativeSelectOption>
          </NativeSelect>
          <p v-if="isEditing" class="text-muted-foreground text-xs">
            Arbetssätt och gruppindelning kan inte ändras efter att uppgiften skapats.
          </p>
        </Field>

        <template v-if="usesGroupRepo(form)">
          <p v-if="isEditing" class="text-muted-foreground text-xs">
            Du kan byta namn och flytta elever. Lägga till eller ta bort grupper går inte efter att
            uppgiften skapats. Elever som redan lämnat in eller fått betyg kan inte flyttas.
          </p>
          <AssignmentGroupEditor
            v-model="form.groups"
            :students="courseStudents"
            :loading="courseStudentsLoading"
            :locked-count="isEditing"
            :error="errors.groups || courseStudentsError"
          />
        </template>

        <Field :data-invalid="!!errors.templateRepo">
          <FieldLabel for="template-repo">Mallrepo</FieldLabel>
          <p v-if="templatesLoading" class="text-muted-foreground text-[13px]" data-test="template-loading">
            Laddar mallrepon…
          </p>
          <NativeSelect
            id="template-repo"
            v-model="form.templateRepo"
            class="w-full font-mono text-[13px]"
            :aria-invalid="!!errors.templateRepo"
            :disabled="templatesLoading || templateOptions.length === 0"
            data-test="template-select"
          >
            <NativeSelectOption value="">
              {{ templatesLoading
                ? 'Laddar mallrepon…'
                : templateOptions.length === 0
                  ? 'Inga mallrepon hittades'
                  : 'Välj mallrepo…' }}
            </NativeSelectOption>
            <NativeSelectOption
              v-for="option in templateOptions"
              :key="option.value"
              :value="option.value"
            >
              {{ option.label }}
            </NativeSelectOption>
          </NativeSelect>
          <FieldError v-if="errors.templateRepo">{{ errors.templateRepo }}</FieldError>
          <p v-if="templatesError" class="text-destructive text-xs" data-test="template-error">
            {{ templatesError }}
          </p>
          <p
            v-if="!templatesLoading && !templatesError && templates.length === 0"
            class="text-muted-foreground text-xs"
            data-test="template-empty"
          >
            Inga mallrepon hittades. Markera ett repo som
            <strong>Template repository</strong> i GitHub och uppdatera listan.
          </p>
          <p
            v-if="templateNotMarked"
            class="text-destructive text-xs"
            data-test="template-not-marked"
          >
            Det valda repot är inte markerat som template i GitHub.
          </p>
          <Button
            v-if="templatesError || (!templatesLoading && templates.length === 0)"
            type="button"
            variant="outline"
            size="sm"
            class="mt-1 w-fit"
            :disabled="templatesLoading"
            @click="loadTemplates(true)"
          >
            Uppdatera mallrepon
          </Button>
          <div v-if="templateCommit" class="text-muted-foreground text-xs" data-test="template-commit">
            <span class="text-foreground font-medium">Senaste commit:</span>
            {{ templateCommit.message }}
            <code class="font-mono">{{ templateCommit.sha.slice(0, 7) }}</code>
            <a
              v-if="selectedTemplate"
              :href="selectedTemplate.htmlUrl"
              target="_blank"
              rel="noopener noreferrer"
              class="text-link ml-1 underline underline-offset-3"
            >Öppna</a>
          </div>
          <p v-else-if="templateCommitLoading" class="text-muted-foreground text-xs">
            Hämtar senaste commit…
          </p>
          <p v-else-if="templateCommitError" class="text-destructive text-xs">
            {{ templateCommitError }}
          </p>
        </Field>
        <Field :data-invalid="!!errors.repoNamePattern">
          <FieldLabel for="repo-name-pattern">
            {{ usesGroupRepo(form)
              ? 'Namn på gruppernas repon'
              : 'Namn på elevernas repon' }}
          </FieldLabel>
          <Input
            id="repo-name-pattern"
            v-model="form.repoNamePattern"
            class="font-mono text-[13px]"
            :placeholder="usesGroupRepo(form)
              ? defaultGroupRepoNamePattern
              : defaultRepoNamePattern"
            :aria-invalid="!!errors.repoNamePattern"
          />
          <p class="text-muted-foreground text-xs">
            Använd platshållarna
            <code class="font-mono">{assignmentSlug}</code>,
            <template v-if="usesGroupRepo(form)">
              <code class="font-mono">{groupName}</code>,
              <code class="font-mono">{groupSlug}</code> eller
              <code class="font-mono">{groupNumber}</code>.
            </template>
            <template v-else>
              <code class="font-mono">{githubLogin}</code>,
              <code class="font-mono">{studentEmail}</code> eller
              <code class="font-mono">{studentId}</code>.
            </template>
          </p>
          <FieldError v-if="errors.repoNamePattern">{{ errors.repoNamePattern }}</FieldError>
          <p
            v-if="repoNamePreview"
            class="text-muted-foreground text-xs"
            data-test="repo-name-preview"
          >
            Blir t.ex. <code class="font-mono text-foreground">{{ repoNamePreview.name }}</code>
          </p>
          <p v-else-if="repoNamePreviewLoading" class="text-muted-foreground text-xs">
            Skapar exempel…
          </p>
          <p v-else-if="repoNamePreviewError" class="text-destructive text-xs" data-test="repo-name-preview-error">
            {{ repoNamePreviewError }}
          </p>
        </Field>

        <div class="flex flex-col gap-2" data-test="requirements">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <div class="flex flex-col gap-0.5">
              <h3 class="text-sm font-semibold">Krav och automatiska tester</h3>
              <p class="text-muted-foreground text-xs">
                Koppla elevtext till testmönster, eller markera kravet som manuellt.
              </p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              :disabled="requirementsLoading || !form.templateRepo"
              data-test="requirements-load"
              @click="loadRequirementsFromTemplate"
            >
              {{ requirementsLoading
                ? 'Läser in…'
                : 'Läs in från mallen' }}
            </Button>
          </div>

          <div
            v-for="(requirement, index) in form.requirements"
            :key="index"
            class="grid gap-2 rounded-lg border p-2 md:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)_auto_auto]"
            data-test="requirement-row"
          >
            <Input
              v-model="requirement.title"
              :aria-label="`Krav ${index + 1}`"
              placeholder="Krav som eleven ser"
            />
            <Input
              v-model="requirement.testPattern"
              :disabled="requirement.isManual"
              class="font-mono text-xs"
              placeholder="Testmönster, t.ex. Release.Tag*"
              :aria-label="`Testmönster ${index + 1}`"
            />
            <label class="flex items-center gap-2 text-xs whitespace-nowrap">
              <Checkbox v-model="requirement.isManual"/>
              Bedöms av lärare
            </label>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              :aria-label="`Ta bort krav ${index + 1}`"
              data-test="requirement-remove"
              @click="removeRequirement(index)"
            >
              <PhTrash/>
            </Button>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            class="w-fit"
            data-test="requirement-add"
            @click="addRequirement"
          >
            <PhPlus/>
            Lägg till krav
          </Button>
          <p v-if="requirementsError" class="text-destructive text-xs" data-test="requirements-error">
            {{ requirementsError }}
          </p>
          <FieldError v-if="errors.requirements">{{ errors.requirements }}</FieldError>
        </div>

        <Alert v-if="unlinkedStudents.length > 0" variant="warning" data-test="unlinked-students">
          <PhWarning/>
          <AlertTitle>
            {{ unlinkedStudents.length }}
            {{ unlinkedStudents.length === 1
              ? 'elev har'
              : 'elever har' }}
            inte kopplat GitHub
          </AlertTitle>
          <AlertDescription>
            <p class="text-xs">
              De får ett repo men kan inte bjudas in förrän de kopplar sitt konto.
            </p>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              class="text-warning hover:bg-warning/10 mt-2 h-7 px-2"
              data-test="unlinked-students-toggle"
              @click="showUnlinkedStudents = !showUnlinkedStudents"
            >
              {{ showUnlinkedStudents
                ? 'Dölj'
                : 'Visa vilka' }}
            </Button>
            <ul v-if="showUnlinkedStudents" class="mt-2 flex flex-col gap-1 text-xs" data-test="unlinked-students-list">
              <li v-for="student in unlinkedStudents" :key="student.id">
                {{ student.name }} · {{ student.email }}
              </li>
            </ul>
          </AlertDescription>
        </Alert>
        <p v-if="unlinkedStudentsLoading" class="text-muted-foreground text-xs">
          Kontrollerar GitHub-kopplingar…
        </p>
        <p v-if="unlinkedStudentsError" class="text-destructive text-xs" data-test="unlinked-students-error">
          {{ unlinkedStudentsError }}
        </p>
      </section>

      <AttachmentList v-if="part" :content-id="part.contentId" :can-manage="canManageAttachments"/>
      <p v-else class="text-muted-foreground text-[13px]">Spara delen först, sedan kan du bifoga filer.</p>
    </form>

    <aside class="bg-muted/40 flex min-w-0 flex-[1_1_280px] flex-col gap-3 rounded-[11px] border p-4 shadow-xs">
      <template v-if="isEditing">
        <h2 class="font-sans text-sm tracking-normal">När du sparar</h2>
        <p class="text-muted-foreground text-[13px]">
          Ändringarna syns direkt för eleverna. Delen behåller sin status.
        </p>
        <p class="text-muted-foreground text-xs">
          Publicera eller avpublicera delen från veckolistan.
        </p>
      </template>
      <template v-else>
        <h2 class="font-sans text-sm tracking-normal">När du publicerar</h2>
        <ol class="text-muted-foreground flex flex-col gap-3 text-[13px]" data-test="publish-steps">
          <li v-for="(step, index) in publishSteps(form)" :key="step" class="flex gap-2.5">
            <span
              class="bg-primary text-primary-foreground flex size-5 shrink-0 items-center justify-center rounded-full text-[11px] font-semibold"
            >{{ index + 1 }}</span>
            <span>{{ step }}</span>
          </li>
        </ol>
        <p class="text-muted-foreground text-xs">Sparar du som utkast syns delen inte för eleverna.</p>
      </template>
    </aside>
  </div>
</template>
