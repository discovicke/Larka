<script setup lang="ts">
import {ref, watch} from 'vue'

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
import {ApiError, api, errorMessage, type Schema} from '@/lib/api'

const props = defineProps<{group: Schema<'GroupDto'>}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{deleted: [group: Schema<'GroupDto'>]}>()

const deleting = ref(false)
const error = ref('')

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

async function remove() {
  deleting.value = true
  error.value = ''
  try {
    await api.DELETE('/api/groups/{id}', {params: {path: {id: props.group.id}}})
    emit('deleted', props.group)
    open.value = false
  } catch (exception) {
    // 409: innehåll är publicerat till gruppen, så den får inte tas bort.
    error.value = exception instanceof ApiError && exception.status === 409
      ? 'Gruppen har publicerat innehåll och kan inte tas bort.'
      : errorMessage(exception)
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="remove">
        <DialogHeader>
          <DialogTitle>Ta bort {{ group.name }}?</DialogTitle>
          <DialogDescription>
            Gruppens medlemskap och kurskopplingar tas bort. Användarna och kurserna finns kvar.
            Det går inte att ångra.
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="error" variant="destructive" data-test="delete-group-error">
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
            data-test="confirm-delete-group"
          >
            Ta bort gruppen
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
