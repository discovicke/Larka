<script setup lang="ts">
import DateTimePicker from '@/components/DateTimePicker.vue'
import {PhChalkboardTeacher, PhFiles, PhGithubLogo, PhTextAa} from '@phosphor-icons/vue'
import {type Component, computed, onMounted, reactive, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import AttachmentList from '@/components/course/AttachmentList.vue'
import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
import PageActions from '@/components/layout/PageActions.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldError,
  FieldLabel,
  FieldLegend,
  FieldSet,
  FieldTitle,
} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {RadioGroup, RadioGroupItem} from '@/components/ui/radio-group'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {ApiError, api, errorMessage, type Schema} from '@/lib/api'
import {partTypeOf, partTypes} from '@/lib/content'
import {
  emptyForm,
  isAssignment,
  isGraded,
  publishSteps,
  toRequests,
  toUpdateRequests,
  usesRepo,
  validate,
} from '@/lib/createContent'
import {toDateTimeLocal} from '@/lib/form'
import NotFoundView from '@/views/NotFoundView.vue'

const submissionModes: {value: Schema<'SubmissionMode'>, label: string, description: string, icon: Component}[] = [
  {value: 'File', label: 'Filer och foton', description: 'Dokument, bilder, mätprotokoll. Funkar från mobilen.', icon: PhFiles},
  {value: 'Text', label: 'Text', description: 'Eleven skriver svaret direkt i RebornLMS.', icon: PhTextAa},
  {value: 'OnSite', label: 'Bedöms på plats', description: 'Du bockar av moment på lektionen, ingen inlämning.', icon: PhChalkboardTeacher},
  {value: 'Repo', label: 'GitHub-repo', description: 'Varje elev får ett eget repo från din mall.', icon: PhGithubLogo},
]

const gradeScales: {value: Schema<'GradeScale'>, label: string}[] = [
  {value: 'PassFail', label: 'IG – G'},
  {value: 'PassFailDistinction', label: 'IG – G – VG'},
]

// Valt kort: mörk ram som i mockupen, i stället för shadcns tonade bakgrund.
const choiceCard = 'has-data-checked:border-primary has-data-checked:bg-transparent has-data-checked:ring-1 has-data-checked:ring-primary'

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

// Felen visas först när läraren försökt spara.
const triedToSave = ref(false)
const errors = computed(() => triedToSave.value
  ? validate(form)
  : {})
const saving = ref(false)
const serverError = ref('')

const module = ref<Schema<'ModuleWithContentDto'>>()
const part = ref<Schema<'ModuleContentItem'>>()
const loading = ref(true)
const notFound = ref(false)
const loadError = ref('')
const currentPartType = computed(() =>
  partTypes.find((candidate) => candidate.value === form.partType) ?? partTypes[0])

const backToCourse = {name: 'course-content', params: {courseId}}

async function load() {
  loading.value = true
  notFound.value = false
  loadError.value = ''
  try {
    const {data} = await api.GET('/api/courses/{id}/modules', {params: {path: {id: courseId}}})
    const foundModule = data?.find((candidate) => candidate.id === moduleId)
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
    const {data: content} = await api.GET('/api/content/{id}', {
      params: {path: {id: foundPart.contentId}},
    })
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

onMounted(load)

function toggleMode(mode: Schema<'SubmissionMode'>, checked: boolean) {
  form.submissionModes = checked
    ? [...form.submissionModes, mode]
    : form.submissionModes.filter((selected) => selected !== mode)
}

async function save(status: Schema<'ContentStatus'> = 'Published') {
  triedToSave.value = true
  if (Object.keys(validate(form)).length > 0) {
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
      await api.PUT('/api/content/{id}', {
        params: {path: {id: current.contentId}},
        body: content,
      })
      await api.PUT('/api/content/{id}/links/{linkId}', {
        params: {path: {id: current.contentId, linkId: current.linkId}},
        body: link,
      })
    } else {
      const {content, link} = toRequests(form, status, {courseId, moduleId})
      await api.POST('/api/content/with-link', {body: {content, link}})
    }

    await router.push(backToCourse)
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
        <RouterLink :to="backToCourse">Avbryt</RouterLink>
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

      <FieldSet>
        <FieldLegend>Typ av del</FieldLegend>
        <div
          v-if="isEditing"
          class="bg-muted/40 flex items-center gap-3 rounded-[11px] border p-3"
          data-test="fixed-part-type"
        >
          <component :is="currentPartType.icon" class="size-5"/>
          <div class="flex flex-col">
            <span class="font-medium">{{ currentPartType.label }}</span>
            <span class="text-muted-foreground text-xs">
              Typen kan inte ändras efter att delen skapats.
            </span>
          </div>
        </div>
        <RadioGroup
          v-else
          v-model="form.partType"
          class="grid grid-cols-[repeat(auto-fit,minmax(min(150px,100%),1fr))] gap-2.5"
        >
          <FieldLabel
            v-for="partType in partTypes"
            :key="partType.value"
            :for="`part-${partType.value}`"
            :class="choiceCard"
          >
            <Field orientation="horizontal">
              <FieldContent>
                <component :is="partType.icon" class="size-5"/>
                <FieldTitle>{{ partType.label }}</FieldTitle>
                <FieldDescription class="text-xs">{{ partType.description }}</FieldDescription>
              </FieldContent>
              <RadioGroupItem :id="`part-${partType.value}`" :value="partType.value"/>
            </Field>
          </FieldLabel>
        </RadioGroup>
      </FieldSet>

      <section class="flex flex-col gap-4 rounded-[11px] border p-5">
        <h2 class="text-base">Grundinfo</h2>
        <Field :data-invalid="!!errors.title">
          <FieldLabel for="title">Titel</FieldLabel>
          <Input id="title" v-model="form.title" :aria-invalid="!!errors.title"/>
          <FieldError v-if="errors.title">{{ errors.title }}</FieldError>
        </Field>
        <Field>
          <FieldLabel for="description">Beskrivning</FieldLabel>
          <MarkdownEditor id="description" v-model="form.bodyMd" label="Beskrivning"/>
        </Field>
        <div class="grid grid-cols-[repeat(auto-fit,minmax(min(200px,100%),1fr))] gap-4">
          <Field>
            <FieldLabel for="publish-at">Publiceras</FieldLabel>
            <DateTimePicker id="publish-at" v-model="form.publishAt" clearable/>
            <FieldDescription class="text-xs">Tomt = direkt.</FieldDescription>
          </Field>
          <Field v-if="isAssignment(form)" :data-invalid="!!errors.dueAt">
            <FieldLabel for="due-at">Slutar</FieldLabel>
            <DateTimePicker
              id="due-at"
              v-model="form.dueAt"
              clearable
              :invalid="!!errors.dueAt"
            />
            <FieldError v-if="errors.dueAt">{{ errors.dueAt }}</FieldError>
          </Field>
          <Field v-if="isGraded(form)">
            <FieldLabel for="grade-scale">Betygsskala</FieldLabel>
            <NativeSelect id="grade-scale" v-model="form.gradeScale" class="w-full">
              <NativeSelectOption v-for="scale in gradeScales" :key="scale.value" :value="scale.value">
                {{ scale.label }}
              </NativeSelectOption>
            </NativeSelect>
          </Field>
        </div>
      </section>

      <section v-if="isAssignment(form)" class="flex flex-col gap-3.5 rounded-[11px] border p-5">
        <div class="flex flex-col">
          <h2 class="text-base">Hur lämnar eleven in?</h2>
          <span class="text-muted-foreground text-[13px]">Välj ett eller flera sätt, eller inget</span>
        </div>
        <div class="grid grid-cols-[repeat(auto-fit,minmax(min(190px,100%),1fr))] gap-2.5">
          <FieldLabel
            v-for="mode in submissionModes"
            :key="mode.value"
            :for="`mode-${mode.value}`"
            :class="choiceCard"
          >
            <Field orientation="horizontal">
              <Checkbox
                :id="`mode-${mode.value}`"
                :model-value="form.submissionModes.includes(mode.value)"
                @update:model-value="(checked) => toggleMode(mode.value, checked === true)"
              />
              <FieldContent>
                <FieldTitle>
                  <component :is="mode.icon"/>
                  {{ mode.label }}
                </FieldTitle>
                <FieldDescription class="text-xs">{{ mode.description }}</FieldDescription>
              </FieldContent>
            </Field>
          </FieldLabel>
        </div>
      </section>

      <!-- Resten av GitHub-inställningarna (reponamn, arbetssätt, regler) kommer i #69. -->
      <section v-if="usesRepo(form)" class="flex flex-col gap-4 rounded-[11px] border p-5">
        <div class="flex items-center gap-3">
          <PhGithubLogo class="size-5"/>
          <div class="flex flex-col">
            <h2 class="text-base">GitHub-inställningar</h2>
            <span class="text-muted-foreground text-[13px]">Varje elev får ett eget privat repo från din mall</span>
          </div>
        </div>
        <Field :data-invalid="!!errors.templateRepo">
          <FieldLabel for="template-repo">Mallrepo</FieldLabel>
          <Input
            id="template-repo"
            v-model="form.templateRepo"
            class="font-mono text-[13px]"
            placeholder="edugrade-net25/mall-lab2-release-pipeline"
            :aria-invalid="!!errors.templateRepo"
          />
          <FieldError v-if="errors.templateRepo">{{ errors.templateRepo }}</FieldError>
        </Field>
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
