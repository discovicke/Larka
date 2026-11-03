<script setup lang="ts">
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {type Schema} from '@/lib/api'
import {unlinkCourseFromGroup} from '@/lib/groupsApi'

const props = defineProps<{groupId: string, groupName: string, course: Schema<'CourseDto'> | null}>()
const emit = defineEmits<{unlinked: [courseId: string]}>()
const open = defineModel<boolean>('open', {required: true})

async function unlink() {
  if (props.course) {
    await unlinkCourseFromGroup(props.groupId, props.course.id)
  }
}
</script>

<template>
  <ConfirmDialog
    v-model:open="open"
    :title="`Koppla bort ${course?.title ?? ''} från ${groupName}?`"
    description="Kursen finns kvar, men gruppens medlemmar når den inte längre via gruppen."
    confirm-label="Koppla bort"
    confirm-test="confirm-unlink-course"
    :action="unlink"
    @done="course && emit('unlinked', course.id)"
  />
</template>
