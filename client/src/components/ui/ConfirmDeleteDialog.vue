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

const props = defineProps<{
  title: string
  description: string
  confirmLabel: string
  confirmTest: string
  errorTest: string
  action: () => Promise<void>
  conflictMessage?: string
}>()
const emit = defineEmits<{done: []}>()
const open = defineModel<boolean>('open', {required: true})

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: remove, loading: deleting, error} = useAsync(async () => {
  try {
    await props.action()
  } catch (exception) {
    throw exception instanceof ApiError && exception.status === 409 && props.conflictMessage
      ? new Error(props.conflictMessage)
      : exception
  }
  emit('done')
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="remove">
        <DialogHeader>
          <DialogTitle>{{ title }}</DialogTitle>
          <DialogDescription>{{ description }}</DialogDescription>
        </DialogHeader>

        <Alert v-if="error" variant="destructive" :data-test="errorTest">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="deleting"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button
            type="submit"
            variant="destructive"
            :disabled="deleting"
            :data-test="confirmTest"
          >
            {{ confirmLabel }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
