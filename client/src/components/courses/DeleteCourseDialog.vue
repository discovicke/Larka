<script setup lang="ts">
import ConfirmDeleteDialog from '@/components/ui/ConfirmDeleteDialog.vue'
import {api, type Schema} from '@/lib/api'

const props = defineProps<{course: Schema<'CourseDto'>}>()
const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{deleted: [course: Schema<'CourseDto'>]}>()

function remove() {
  return api.DELETE('/api/courses/{id}', {params: {path: {id: props.course.id}}}).then(() => undefined)
}
</script>

<template>
  <ConfirmDeleteDialog
    v-model:open="open"
    :title="`Ta bort ${course.title}?`"
    description="Kursens veckor, innehåll och kopplingar till grupper tas bort. Grupperna och användarna finns kvar. Det går inte att ångra."
    confirm-label="Ta bort kursen"
    confirm-test="confirm-delete-course"
    error-test="delete-course-error"
    conflict-message="Kursen har elevarbete och kan inte tas bort."
    :action="remove"
    @done="emit('deleted', course)"
  />
</template>
