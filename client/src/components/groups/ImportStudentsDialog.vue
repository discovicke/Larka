<script setup lang="ts">
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {FieldDescription, FieldLabel} from '@/components/ui/field'
import {Textarea} from '@/components/ui/textarea'
import {api, errorMessage, unwrap, type Schema} from '@/lib/api'
import {importOutcomeLabels, parseStudentLines, previewLines, wasAdded} from '@/lib/importStudents'

const props = defineProps<{groupId: string, groupName: string, memberIds: string[]}>()
const emit = defineEmits<{added: [members: Schema<'GroupMemberDto'>[]]}>()

const open = defineModel<boolean>('open', {required: true})

const users = ref<Schema<'UserDto'>[]>([])
const text = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref('')
const result = ref<Schema<'ImportStudentsResultDto'> | null>(null)

const lines = computed(() => parseStudentLines(text.value))
const unreadable = computed(() => lines.value.filter((line) => line.unreadable))
const preview = computed(() => previewLines(lines.value, users.value, props.memberIds))
const existing = computed(() => preview.value.filter((line) => line.kind === 'existing'))
const created = computed(() => preview.value.filter((line) => line.kind === 'new'))
const skipped = computed(() => preview.value.filter((line) => line.kind === 'skipped'))
const toAdd = computed(() => existing.value.length + created.value.length)

const groups = computed(() => [
  {key: 'existing', label: 'Finns redan, läggs till i klassen', rows: existing.value.map((line) => ({
    key: line.email, title: line.name, detail: line.email,
  }))},
  {key: 'new', label: 'Nya, skapas utan lösenord', rows: created.value.map((line) => ({
    key: line.email, title: line.name, detail: line.email,
  }))},
  {key: 'skipped', label: 'Hoppas över', rows: skipped.value.map((line) => ({
    key: `${line.number}-${line.email}`, title: line.name, detail: `${line.email}. ${line.note}`,
  }))},
  {key: 'unreadable', label: 'Går inte att läsa', rows: unreadable.value.map((line) => ({
    key: String(line.number),
    title: `Rad ${line.number}`,
    detail: `${line.raw}. Skriv namn och e-post på samma rad.`,
  }))},
].filter((group) => group.rows.length > 0))

const report = computed(() => (result.value?.lines ?? []).map((line, index) => ({
  key: `${index}-${line.email}`,
  title: line.name || line.email,
  detail: line.name
    ? line.email
    : '',
  outcome: importOutcomeLabels[line.outcome],
  done: wasAdded(line.outcome),
})))
const addedCount = computed(() => report.value.filter((line) => line.done).length)

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  text.value = ''
  error.value = ''
  result.value = null
  loading.value = true
  try {
    users.value = unwrap(await api.GET('/api/users'))
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
})

async function submit() {
  if (toAdd.value === 0) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    // Alla rader skickas, så att redovisningen visar vad som hände med varje rad.
    const response = unwrap(await api.POST('/api/groups/{id}/members/import', {
      params: {path: {id: props.groupId}},
      body: {lines: lines.value.map((line) => ({
        name: line.unreadable && !line.email
          ? line.raw
          : line.name,
        email: line.email,
      }))},
    }))
    result.value = response
    emit('added', response.added)
  } catch (exception) {
    error.value = `Ingen i listan lades till. ${errorMessage(exception)}`
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[calc(100dvh-2rem)] grid-cols-[minmax(0,1fr)] overflow-y-auto sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" novalidate @submit.prevent="submit">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Klistra in elever i {{ groupName }}</DialogTitle>
          <DialogDescription>
            En person per rad, med namn och e-post åtskilda av komma eller tabb. Alla läggs till som elever.
          </DialogDescription>
        </DialogHeader>

        <template v-if="result">
          <p class="text-sm" data-test="import-summary">
            {{ addedCount }} av {{ report.length }} rader lades till i klassen.
          </p>
          <ul class="max-h-64 overflow-y-auto rounded-[11px] border" data-test="import-report">
            <li
              v-for="line in report"
              :key="line.key"
              class="flex flex-col gap-0.5 border-b px-4 py-2.5 last:border-b-0"
              data-test="import-report-row"
            >
              <span class="font-medium">{{ line.title }}</span>
              <span class="text-muted-foreground text-[13px]">
                {{ line.outcome }}<template v-if="line.detail"> · {{ line.detail }}</template>
              </span>
            </li>
          </ul>
        </template>

        <template v-else>
          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="import-text">Lista</FieldLabel>
            <Textarea
              id="import-text"
              v-model="text"
              rows="5"
              class="w-full font-mono text-[13px]"
              placeholder="Siv Student, siv@skola.se"
              :disabled="loading"
              data-test="import-text"
            />
            <FieldDescription>Nya elever skapas utan lösenord. De kan inte logga in förrän de har fått ett.</FieldDescription>
          </div>

          <div
            v-if="groups.length > 0"
            class="flex min-w-0 flex-col gap-3"
            data-test="import-preview"
          >
            <section
              v-for="group in groups"
              :key="group.key"
              :aria-label="group.label"
              class="flex flex-col gap-1.5"
              :data-test="`import-${group.key}`"
            >
              <h3 class="text-muted-foreground text-sm font-medium">{{ group.label }} ({{ group.rows.length }})</h3>
              <ul class="rounded-[11px] border">
                <li
                  v-for="row in group.rows"
                  :key="row.key"
                  class="flex flex-col gap-0.5 border-b px-4 py-2.5 last:border-b-0"
                  data-test="import-row"
                >
                  <span class="font-medium break-words">{{ row.title }}</span>
                  <span class="text-muted-foreground text-[13px] break-words">{{ row.detail }}</span>
                </li>
              </ul>
            </section>
          </div>
        </template>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            v-if="result"
            type="button"
            data-test="import-close"
            @click="open = false"
          >
            Stäng
          </Button>
          <template v-else>
            <Button
              type="button"
              variant="outline"
              :disabled="saving"
              @click="open = false"
            >
              Avbryt
            </Button>
            <Button
              type="submit"
              :disabled="saving || toAdd === 0"
              data-test="confirm-import"
            >
              {{ toAdd === 1
                ? 'Lägg till 1 elev'
                : `Lägg till ${toAdd} elever` }}
            </Button>
          </template>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
