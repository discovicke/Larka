<script setup lang="ts">
import {reactive} from 'vue'

import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'

const form = reactive({groupId: '', courseId: ''})
const {result, busy, submit: run, fail} = useDevSubmit()

async function submit() {
  if (!form.groupId || !form.courseId) {
    fail('Pick a group and a course.')
    return
  }

  await run(() => api.POST('/api/groups/{id}/courses/{courseId}', {
    params: {path: {id: form.groupId, courseId: form.courseId}},
  }))
}
</script>

<template>
  <DevFormCard
    title="Link course to group"
    description="The group reads the course."
    submit-label="Link"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="link-group"
      v-model="form.groupId"
      label="Group"
      source="groups"
    />
    <EntitySelect
      id="link-course"
      v-model="form.courseId"
      label="Course"
      source="courses"
    />
  </DevFormCard>
</template>
