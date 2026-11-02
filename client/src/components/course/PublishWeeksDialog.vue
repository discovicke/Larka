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
import {api, errorMessage, type Schema} from '@/lib/api'
import {countLabel, draftCount, formatDate} from '@/lib/content'

const props = defineProps<{
  courseId: string
  // Veckorna som har utkast.
  weeks: Schema<'ModuleWithContentDto'>[]
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{published: [result: Schema<'PublishWeeksResultDto'>]}>()

const saving = ref(false)
const error = ref('')

const publishLabel = computed(() => `Publicera ${countLabel(props.weeks.length, 'vecka', 'veckor')}`)

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

function dateOf(week: Schema<'ModuleWithContentDto'>): string {
  return week.publishAt && new Date(week.publishAt) > new Date()
    ? formatDate(week.publishAt)
    : 'Direkt'
}

async function publish() {
  saving.value = true
  error.value = ''
  try {
    const {data} = await api.POST('/api/courses/{id}/modules/publish', {
      params: {path: {id: props.courseId}},
      body: {moduleIds: props.weeks.map((week) => week.id)},
    })
    open.value = false
    if (data) {
      emit('published', data)
    }
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[calc(100dvh-2rem)] grid-cols-[minmax(0,1fr)] overflow-y-auto">
      <DialogHeader class="min-w-0">
        <DialogTitle>Publicera alla veckor?</DialogTitle>
        <DialogDescription>Varje vecka och dess delar syns för eleverna på veckans datum.</DialogDescription>
      </DialogHeader>

      <ul class="overflow-hidden rounded-lg border" data-test="publish-weeks">
        <li
          v-for="week in weeks"
          :key="week.id"
          class="flex flex-wrap items-center justify-between gap-x-3 gap-y-0.5 border-b px-3 py-2 last:border-b-0"
          data-test="publish-week"
        >
          <span class="min-w-0 text-sm break-words">{{ week.title }}</span>
          <span class="text-muted-foreground text-[13px]">
            {{ dateOf(week) }} · {{ countLabel(draftCount(week), 'del', 'delar') }}
          </span>
        </li>
      </ul>

      <p class="text-muted-foreground text-sm">Du kan ändra datum eller avpublicera en del när som helst.</p>

      <Alert v-if="error" variant="destructive">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>

      <DialogFooter>
        <Button variant="outline" :disabled="saving" @click="open = false">Avbryt</Button>
        <Button :disabled="saving" data-test="confirm-publish" @click="publish">{{ publishLabel }}</Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
