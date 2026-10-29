<script setup lang="ts">
import {PhFile, PhPaperPlaneTilt, PhWarning, PhX} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Input} from '@/components/ui/input'
import {api, errorMessage, multipart, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {formatSize} from '@/lib/submissions'

// Elevens inlämning av en uppgift. Formuläret följer uppgiftens inlämningssätt: text och/eller filer.
// Repo visas av GitHub-blocket i vyn och bedömning på plats har ingen inlämning från eleven.
const props = defineProps<{item: Schema<'StudentContentItem'>}>()
const emit = defineEmits<{submitted: []}>()

const submission = ref<Schema<'SubmissionDto'> | null>(null)
const bodyMd = ref('')
const keptFiles = ref<Schema<'SubmissionFileDto'>[]>([])
const addedFiles = ref<File[]>([])
const editing = ref(false)
const saving = ref(false)
const error = ref('')
// Editorn läser bara sitt startvärde, så den monteras om när formuläret fylls i på nytt.
const editorKey = ref(0)

const hasText = computed(() => props.item.submissionModes.includes('Text'))
const hasFiles = computed(() => props.item.submissionModes.includes('File'))
const hasForm = computed(() => hasText.value || hasFiles.value)
const onSite = computed(() => props.item.submissionModes.includes('OnSite'))
const status = computed(() => props.item.submissionStatus ?? 'NotStarted')
// Den som inte lämnat in, eller ska komplettera, ser formuläret direkt. En inlämnad kan välja att ändra.
const formOpen = computed(() => hasForm.value
  && (status.value === 'NotStarted' || status.value === 'NeedsWork' || editing.value))
const isLate = computed(() => !!props.item.dueAt
  && status.value !== 'Graded'
  && new Date(props.item.dueAt) < new Date())
const submitLabel = computed(() => status.value === 'NotStarted'
  ? 'Lämna in'
  : status.value === 'NeedsWork'
    ? 'Lämna in igen'
    : 'Spara ändringarna')
const canSubmit = computed(() => (hasText.value && bodyMd.value.trim() !== '')
  || (hasFiles.value && keptFiles.value.length + addedFiles.value.length > 0))

watch(() => props.item.linkId, load, {immediate: true})

async function load() {
  submission.value = null
  editing.value = false
  error.value = ''
  if (!hasForm.value) {
    return
  }
  try {
    const {data} = await api.GET('/api/links/{id}/submissions', {params: {path: {id: props.item.linkId}}})
    submission.value = data?.[0] ?? null
  } catch (exception) {
    error.value = errorMessage(exception)
  }
  fillForm()
}

function fillForm() {
  bodyMd.value = submission.value?.bodyMd ?? ''
  keptFiles.value = [...submission.value?.files ?? []]
  addedFiles.value = []
  editorKey.value++
}

function startEditing() {
  fillForm()
  editing.value = true
}

function cancelEditing() {
  editing.value = false
}

function onFilesChosen(event: Event) {
  const input = event.target as HTMLInputElement
  addedFiles.value = [...addedFiles.value, ...Array.from(input.files ?? [])]
  input.value = ''
}

async function submit() {
  saving.value = true
  error.value = ''
  const body = new FormData()
  if (hasText.value && bodyMd.value.trim() !== '') {
    body.append('bodyMd', bodyMd.value)
  }
  if (hasFiles.value) {
    // Utan keepFileIds ersätter backend alla filer, så de som ska vara kvar anges alltid.
    for (const file of keptFiles.value) {
      body.append('keepFileIds', file.id)
    }
    for (const file of addedFiles.value) {
      body.append('files', file, file.name)
    }
  }
  try {
    const {data} = await api.POST('/api/links/{id}/submissions', {
      params: {path: {id: props.item.linkId}},
      body: multipart<Schema<'SubmitForm'>>(body),
    })
    if (data) {
      submission.value = data
    }
    editing.value = false
    fillForm()
    emit('submitted')
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <section
    v-if="hasForm || onSite"
    class="bg-card flex flex-col gap-4 rounded-xl border p-4"
    aria-labelledby="submission-h"
    data-test="submission-panel"
  >
    <div class="flex flex-wrap items-center justify-between gap-2">
      <h2 id="submission-h" class="text-lg font-semibold">Din inlämning</h2>
      <Badge
        v-if="isLate"
        variant="outline"
        class="border-warning/20 bg-warning/10 text-warning dark:border-warning/30 dark:bg-warning/20"
        data-test="submission-late"
      >
        <PhWarning/>
        Sen inlämning
      </Badge>
    </div>

    <p v-if="onSite" class="text-muted-foreground text-sm" data-test="submission-onsite">
      Bedöms på plats av läraren.
    </p>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <form
      v-if="formOpen"
      class="flex flex-col gap-4"
      data-test="submission-form"
      @submit.prevent="submit"
    >
      <div v-if="hasText" class="flex flex-col gap-1.5">
        <span class="text-sm font-medium">Ditt svar</span>
        <MarkdownEditor
          :key="editorKey"
          id="submission-body"
          v-model="bodyMd"
          label="Ditt svar"
        />
      </div>

      <div v-if="hasFiles" class="flex flex-col gap-2">
        <label for="submission-files" class="text-sm font-medium">Filer och foton</label>
        <ul v-if="keptFiles.length + addedFiles.length > 0" class="flex flex-col gap-1.5" data-test="submission-file-list">
          <li
            v-for="file in keptFiles"
            :key="file.id"
            class="flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm"
            data-test="submission-file"
          >
            <PhFile class="text-muted-foreground size-4 shrink-0"/>
            <span class="min-w-0 flex-1 truncate">{{ file.fileName }}</span>
            <span class="text-muted-foreground text-xs">{{ formatSize(file.sizeBytes) }}</span>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              :aria-label="`Ta bort ${file.fileName}`"
              data-test="submission-file-remove"
              @click="keptFiles = keptFiles.filter((kept) => kept.id !== file.id)"
            >
              <PhX/>
            </Button>
          </li>
          <li
            v-for="file in addedFiles"
            :key="file.name + file.size + file.lastModified"
            class="flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm"
            data-test="submission-file"
          >
            <PhFile class="text-muted-foreground size-4 shrink-0"/>
            <span class="min-w-0 flex-1 truncate">{{ file.name }}</span>
            <span class="text-muted-foreground text-xs">{{ formatSize(file.size) }}</span>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              :aria-label="`Ta bort ${file.name}`"
              data-test="submission-file-remove"
              @click="addedFiles = addedFiles.filter((added) => added !== file)"
            >
              <PhX/>
            </Button>
          </li>
        </ul>
        <Input
          id="submission-files"
          type="file"
          multiple
          accept=".pdf,.png,.jpg,.jpeg,.gif,.webp,.txt,.md,.docx,.pptx,.xlsx,.zip"
          data-test="submission-file-input"
          @change="onFilesChosen"
        />
        <p class="text-muted-foreground text-xs">Pdf, bilder, text, Office-filer eller zip. Högst 20 MB per fil.</p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <Button type="submit" :disabled="saving || !canSubmit" data-test="submission-submit">
          <PhPaperPlaneTilt/>
          {{ submitLabel }}
        </Button>
        <Button
          v-if="editing"
          type="button"
          variant="ghost"
          data-test="submission-cancel"
          @click="cancelEditing"
        >
          Avbryt
        </Button>
        <span v-if="isLate" class="text-muted-foreground text-xs">Deadline har passerat, men du kan fortfarande lämna in.</span>
      </div>
    </form>

    <div v-else-if="hasForm && submission" class="flex flex-col gap-3" data-test="submission-view">
      <p v-if="submission.submittedAt" class="text-muted-foreground text-sm" data-test="submission-date">
        Inlämnad {{ formatDate(submission.submittedAt, true) }}
      </p>
      <MarkdownView v-if="submission.bodyMd" :key="submission.submittedAt ?? ''" :markdown="submission.bodyMd"/>
      <ul v-if="submission.files.length > 0" class="flex flex-col gap-1.5">
        <li
          v-for="file in submission.files"
          :key="file.id"
          class="flex items-center gap-2 rounded-md border px-3 py-1.5 text-sm"
          data-test="submission-file"
        >
          <PhFile class="text-muted-foreground size-4 shrink-0"/>
          <a
            :href="file.url"
            target="_blank"
            rel="noopener"
            class="min-w-0 flex-1 truncate underline underline-offset-3"
          >{{ file.fileName }}</a>
          <span class="text-muted-foreground text-xs">{{ formatSize(file.sizeBytes) }}</span>
        </li>
      </ul>
      <div v-if="status === 'Submitted'">
        <Button variant="outline" data-test="submission-edit" @click="startEditing">Ändra inlämning</Button>
      </div>
    </div>
  </section>
</template>
