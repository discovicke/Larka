<script setup lang="ts">
import {PhFile, PhPaperPlaneTilt, PhX} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import MarkdownEditor from '@/components/editor/MarkdownEditor.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Input} from '@/components/ui/input'
import TextLimitNote from '@/components/TextLimitNote.vue'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useToast} from '@/composables/useToast'
import {api, errorMessage, multipart, type Schema} from '@/lib/api'
import {UPLOAD_ACCEPT, uploadProblem} from '@/lib/uploads'
import {formatDate} from '@/lib/content'
import {formatSize} from '@/lib/material'
import {clearDraft, loadDraft, saveDraft} from '@/lib/submissionDraft'
import {fetchLinkSubmissions} from '@/lib/submissionsApi'
import {isTooLong, textLimits} from '@/lib/textLimits'

// Elevens inlämning av en uppgift. Formuläret följer uppgiftens inlämningssätt: text och/eller filer.
// Repo visas av GitHub-blocket i vyn och bedömning på plats har ingen inlämning från eleven.
const props = defineProps<{item: Schema<'StudentContentItem'>}>()
const emit = defineEmits<{submitted: []}>()

const {showToast} = useToast()
const {me} = useCurrentUser()
const submission = ref<Schema<'SubmissionDto'> | null>(null)
const bodyMd = ref('')
const keptFiles = ref<Schema<'SubmissionFileDto'>[]>([])
const addedFiles = ref<File[]>([])
const editing = ref(false)
// Editorn läser bara sitt startvärde, så den monteras om när formuläret fylls i på nytt.
const editorKey = ref(0)
// Formuläret visas först när inlämningen har hämtats. Annars nollställer fillForm det eleven hunnit skriva.
const loaded = ref(false)
// Sant när texten i formuläret är ett utkast från webbläsaren och inte det som lämnats in.
const draftRestored = ref(false)

const hasText = computed(() => props.item.submissionModes.includes('Text'))
const hasFiles = computed(() => props.item.submissionModes.includes('File'))
const hasRepo = computed(() => props.item.submissionModes.includes('Repo'))
const repoLocked = computed(() => hasRepo.value && Boolean(props.item.repo?.lockedAt))
const repoHasSubmission = computed(() =>
  Boolean(props.item.repo?.submissionSha) || status.value === 'Submitted')
const hasForm = computed(() => hasText.value || hasFiles.value)
const onSite = computed(() => props.item.submissionModes.includes('OnSite'))
const status = computed(() => props.item.submissionStatus ?? 'NotStarted')
// Den som inte lämnat in, eller ska komplettera, ser formuläret direkt. En inlämnad kan välja att ändra.
// Backend räknar fönstret. Utan läge (inte en uppgift) är inlämningen öppen utan deadline.
const windowState = computed(() => {
  const deadline = props.item.deadline
  return {
    open: deadline?.canHandIn ?? true,
    until: deadline?.handInUntil ?? null,
    closed: deadline?.handInClosed ?? null,
  }
})
const formOpen = computed(() => hasForm.value
  && loaded.value
  && windowState.value.open
  && (status.value === 'NotStarted' || status.value === 'NeedsWork' || editing.value))
// Eleven får veta till när hen kan lämna in, eller varför det är stängt.
const windowText = computed(() => {
  const state = windowState.value
  if (state.open && state.until) {
    if (status.value === 'Submitted') {
      return `Du kan ändra din inlämning till ${formatDate(state.until, true)}.`
    }
    return status.value === 'NeedsWork'
      ? `Komplettera senast ${formatDate(state.until, true)}.`
      : `Du kan lämna in till ${formatDate(state.until, true)}.`
  }
  // Låst efter huvuddeadlinen. Eleven kan ha en egen deadline eller en kompletteringsdeadline kvar, så texten
  // säger inte att deadlinen har gått ut.
  if (state.closed === 'Locked') {
    return 'Inlämnad. Den är låst tills läraren har bedömt den.'
  }
  if (state.closed !== 'Closed') {
    return ''
  }
  if (props.item.deadline?.autoFail) {
    return status.value === 'NeedsWork'
      ? 'Sista deadline har passerat och du lämnade inte in någon komplettering, så du har fått IG automatiskt.'
      : 'Sista deadline har passerat och du lämnade inte in, så du har fått IG automatiskt.'
  }
  return 'Det går inte längre att lämna in. Sista deadline har passerat.'
})
const submitLabel = computed(() => status.value === 'NotStarted'
  ? 'Lämna in'
  : status.value === 'NeedsWork'
    ? 'Lämna in igen'
    : 'Spara ändringarna')
const repoSubmitLabel = computed(() => status.value === 'NeedsWork'
  ? 'Lämna in igen'
  : 'Lämna in senaste commit')
// Med repo och text eller filer lämnar formulärets knapp in allt, både senaste commit och svaret. Då finns ingen
// egen repo-knapp, så att eleven inte ser två knappar som gör samma sak.
const formSubmitLabel = computed(() => hasRepo.value && status.value !== 'Submitted'
  ? repoSubmitLabel.value
  : submitLabel.value)
const textTooLong = computed(() => hasText.value && isTooLong(bodyMd.value, textLimits.submissionText))
const canSubmit = computed(() => !textTooLong.value && (hasRepo.value
  || (hasText.value && bodyMd.value.trim() !== '')
  || (hasFiles.value && keptFiles.value.length + addedFiles.value.length > 0)))

const {run: submit, loading: saving, error} = useAsync(async () => {
  const doneText = status.value === 'NotStarted'
    ? 'Uppgiften är inlämnad.'
    : status.value === 'NeedsWork'
      ? 'Uppgiften är inlämnad igen.'
      : 'Ändringarna är sparade.'
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
  const {data} = await api.POST('/api/links/{id}/submissions', {
    params: {path: {id: props.item.linkId}},
    body: multipart<Schema<'SubmitForm'>>(body),
  })
  if (data) {
    submission.value = data
  }
  clearDraft(me.value?.id, props.item.linkId)
  editing.value = false
  fillForm()
  emit('submitted')
  showToast(doneText)
})

watch(() => props.item.linkId, load, {immediate: true})

// Texten sparas som utkast medan formuläret är öppet. Samma text som den inlämnade behöver inget utkast.
watch(bodyMd, (body) => {
  if (!formOpen.value || !hasText.value) {
    return
  }
  if (body === (submission.value?.bodyMd ?? '')) {
    clearDraft(me.value?.id, props.item.linkId)
  } else {
    saveDraft(me.value?.id, props.item.linkId, body)
  }
})

async function load() {
  const linkId = props.item.linkId
  submission.value = null
  editing.value = false
  error.value = ''
  loaded.value = false
  if (!hasForm.value && !hasRepo.value) {
    loaded.value = true
    return
  }
  try {
    const submissions = await fetchLinkSubmissions(linkId)
    if (linkId !== props.item.linkId) {
      return
    }
    submission.value = submissions[0] ?? null
  } catch (exception) {
    error.value = errorMessage(exception)
  }
  fillForm()
  loaded.value = true
}

function fillForm() {
  const saved = submission.value?.bodyMd ?? ''
  // Ett utkast tas bara fram när eleven kan skriva, så att en låst eller bedömd inlämning visas som den är.
  const canWrite = hasText.value
    && windowState.value.open
    && (editing.value || status.value === 'NotStarted' || status.value === 'NeedsWork')
  const draft = canWrite
    ? loadDraft(me.value?.id, props.item.linkId)
    : null
  draftRestored.value = draft !== null && draft !== saved
  bodyMd.value = draftRestored.value
    ? draft!
    : saved
  keptFiles.value = [...submission.value?.files ?? []]
  addedFiles.value = []
  editorKey.value++
}

function startEditing() {
  editing.value = true
  fillForm()
}

function cancelEditing() {
  clearDraft(me.value?.id, props.item.linkId)
  draftRestored.value = false
  editing.value = false
}

function discardDraft() {
  clearDraft(me.value?.id, props.item.linkId)
  draftRestored.value = false
  bodyMd.value = submission.value?.bodyMd ?? ''
  editorKey.value++
}

function onFilesChosen(event: Event) {
  const input = event.target as HTMLInputElement
  const chosen = [...addedFiles.value, ...Array.from(input.files ?? [])]
  input.value = ''
  // Fel filtyp, för stora eller för många filer sägs direkt, i stället för när eleven trycker Lämna in.
  const problem = uploadProblem([...chosen])
  error.value = problem ?? ''
  if (!problem) {
    addedFiles.value = chosen
  }
}

</script>

<template>
  <section
    v-if="hasForm || hasRepo || onSite"
    class="bg-card flex flex-col gap-4 rounded-xl border p-4"
    aria-labelledby="submission-h"
    data-test="submission-panel"
  >
    <h2 id="submission-h" class="text-lg font-semibold">Din inlämning</h2>

    <!-- Vid komplettering står lärarens återkoppling först, direkt ovanför det eleven ska lämna in. -->
    <div
      v-if="status === 'NeedsWork' && submission?.grade?.feedback"
      class="border-warning/30 bg-warning/10 flex flex-col gap-1 rounded-lg border p-3"
      data-test="submission-needs-work"
    >
      <p class="text-sm font-semibold">
        {{ item.deadline?.autoFail
          ? 'Lärarens återkoppling'
          : 'Läraren vill att du kompletterar' }}
      </p>
      <p class="text-sm leading-relaxed whitespace-pre-wrap wrap-anywhere">{{ submission.grade.feedback }}</p>
      <p class="text-muted-foreground text-xs">{{ formatDate(submission.grade.gradedAt, true) }}</p>
    </div>

    <p
      v-if="windowText && (hasForm || hasRepo)"
      class="text-sm font-medium"
      :data-test="windowState.open
        ? 'submission-open-until'
        : 'submission-closed'"
    >
      {{ windowText }}
    </p>

    <p v-if="onSite" class="text-muted-foreground text-sm" data-test="submission-onsite">
      Bedöms på plats av läraren.
    </p>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-if="hasRepo" class="flex flex-col gap-2" data-test="repo-submission">
      <p v-if="repoHasSubmission" class="text-muted-foreground text-sm">
        Inlämnad
        <template v-if="submission?.submittedAt">
          {{ formatDate(submission.submittedAt, true) }}
        </template>
      </p>
      <p v-else-if="repoLocked" class="text-muted-foreground text-sm" data-test="repo-submission-locked">
        Repot låstes vid deadline. Ingen commit registrerades som inlämning.
      </p>
      <p v-else class="text-muted-foreground text-sm">
        När du lämnar in sparas senaste commit som din inlämning. Du kan lämna in igen fram till deadline.
      </p>
      <Button
        v-if="windowState.open && !repoLocked && !formOpen"
        type="button"
        :disabled="saving"
        data-test="repo-submission-submit"
        @click="submit"
      >
        <PhPaperPlaneTilt/>
        {{ repoSubmitLabel }}
      </Button>
    </div>

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
          tall
        />
        <TextLimitNote :text="bodyMd" :max="textLimits.submissionText"/>
        <div
          v-if="draftRestored"
          class="flex flex-wrap items-center gap-x-2 text-sm"
          data-test="submission-draft-restored"
        >
          <span>Texten du skrev senast är återställd. Den är inte inlämnad än.</span>
          <Button
            type="button"
            variant="link"
            size="sm"
            class="h-auto px-0"
            data-test="submission-draft-discard"
            @click="discardDraft"
          >
            Släng utkastet
          </Button>
        </div>
        <p v-else class="text-muted-foreground text-xs" data-test="submission-draft-hint">
          Texten sparas som utkast i den här webbläsaren medan du skriver.
        </p>
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
          :accept="UPLOAD_ACCEPT"
          data-test="submission-file-input"
          @change="onFilesChosen"
        />
        <p class="text-muted-foreground text-xs">Pdf, bilder, text, Office-filer eller zip. Högst 20 MB per fil.</p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <Button type="submit" :disabled="saving || !canSubmit" data-test="submission-submit">
          <PhPaperPlaneTilt/>
          {{ formSubmitLabel }}
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
      <div v-if="status === 'Submitted' && windowState.open">
        <Button variant="outline" data-test="submission-edit" @click="startEditing">Ändra inlämning</Button>
      </div>
    </div>
  </section>
</template>
