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

const props = defineProps<{course: Schema<'CourseDto'>}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{deleted: [course: Schema<'CourseDto'>]}>()

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: remove, loading: deleting, error} = useAsync(async () => {
  try {
    await api.DELETE('/api/courses/{id}', {params: {path: {id: props.course.id}}})
  } catch (exception) {
    // 409: det finns elevarbete i kursen, så den får inte tas bort.
    throw exception instanceof ApiError && exception.status === 409
      ? new Error('Kursen har elevarbete och kan inte tas bort.')
      : exception
  }
  emit('deleted', props.course)
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="remove">
        <DialogHeader>
          <DialogTitle>Ta bort {{ course.title }}?</DialogTitle>
          <DialogDescription>
            Kursens veckor, innehåll och kopplingar till grupper tas bort. Grupperna och användarna finns kvar.
            Det går inte att ångra.
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="error" variant="destructive" data-test="delete-course-error">
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
            data-test="confirm-delete-course"
          >
            Ta bort kursen
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
