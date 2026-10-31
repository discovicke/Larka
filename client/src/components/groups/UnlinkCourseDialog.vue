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
import {type Schema} from '@/lib/api'
import {unlinkCourseFromGroup} from '@/lib/groupsApi'

const props = defineProps<{groupId: string, groupName: string, course: Schema<'CourseDto'> | null}>()
const emit = defineEmits<{unlinked: [courseId: string]}>()

const open = defineModel<boolean>('open', {required: true})

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: unlink, loading: removing, error} = useAsync(async () => {
  if (!props.course) {
    return
  }

  await unlinkCourseFromGroup(props.groupId, props.course.id)
  emit('unlinked', props.course.id)
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-md">
      <DialogHeader class="min-w-0">
        <DialogTitle class="break-words">Koppla bort {{ course?.title }} från {{ groupName }}?</DialogTitle>
        <DialogDescription>Kursen finns kvar, men gruppens medlemmar når den inte längre via gruppen.</DialogDescription>
      </DialogHeader>

      <Alert v-if="error" variant="destructive">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>

      <DialogFooter>
        <Button
          type="button"
          variant="outline"
          :disabled="removing"
          @click="open = false"
        >
          Avbryt
        </Button>
        <Button
          type="button"
          variant="destructive"
          :disabled="removing"
          data-test="confirm-unlink-course"
          @click="unlink"
        >
          Koppla bort
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
