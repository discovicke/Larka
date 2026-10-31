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
import {ApiError, api, type Schema} from '@/lib/api'

const props = defineProps<{group: Schema<'GroupDto'>}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{deleted: [group: Schema<'GroupDto'>]}>()

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: remove, loading: deleting, error} = useAsync(async () => {
  try {
    await api.DELETE('/api/groups/{id}', {params: {path: {id: props.group.id}}})
  } catch (exception) {
    // 409: innehåll är publicerat till gruppen, så den får inte tas bort.
    throw exception instanceof ApiError && exception.status === 409
      ? new Error('Gruppen har publicerat innehåll och kan inte tas bort.')
      : exception
  }
  emit('deleted', props.group)
  open.value = false
})
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
