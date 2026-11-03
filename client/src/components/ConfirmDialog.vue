<script setup lang="ts">
import {watch} from 'vue'

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
import {useAsync} from '@/composables/useAsync'
import {ApiError} from '@/lib/api'

// En bekräftelse före en åtgärd som tar bort eller kopplar loss något. Dialogen stängs när åtgärden lyckas, och ett
// fel visas i dialogen. Ett 409 kan få en egen text (conflictMessage).
const props = defineProps<{
  title: string
  description: string
  confirmLabel: string
  confirmTest: string
  errorTest?: string
  action: () => Promise<void>
  conflictMessage?: string
  // Röd knapp för det som tar bort något, vanlig för det som går att ändra tillbaka.
  confirmVariant?: 'destructive' | 'default'
  // Texten när felet inte har någon egen, t.ex. "Kunde inte spara. Försök igen."
  failureText?: string
}>()
const emit = defineEmits<{done: []}>()
const open = defineModel<boolean>('open', {required: true})

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: confirm, loading: working, error} = useAsync(async () => {
  try {
    await props.action()
  } catch (exception) {
    throw exception instanceof ApiError && exception.status === 409 && props.conflictMessage
      ? new Error(props.conflictMessage)
      : exception
  }
  emit('done')
  open.value = false
}, props.failureText)
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-md">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="confirm">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">{{ title }}</DialogTitle>
          <DialogDescription>{{ description }}</DialogDescription>
        </DialogHeader>

        <Alert v-if="error" variant="destructive" :data-test="errorTest">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="working"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button
            type="submit"
            :variant="confirmVariant ?? 'destructive'"
            :disabled="working"
            :data-test="confirmTest"
          >
            {{ confirmLabel }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
