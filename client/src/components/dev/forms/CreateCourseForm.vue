<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'

const form = reactive({schoolId: '', title: '', code: ''})
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember} = useDevOptions()

async function submit() {
  if (!form.schoolId) {
    fail('Pick a school.')
    return
  }

  const course = await run(() => api.POST('/api/courses', {
    body: {schoolId: form.schoolId, title: form.title, code: form.code},
  }))
  if (course) {
    remember('courses', {value: course.id, label: `${course.code} ${course.title}`})
  }
}
</script>

<template>
  <DevFormCard
    title="Course"
    description="Link it to a group below so the group reads it."
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="course-school"
      v-model="form.schoolId"
      label="School"
      source="schools"
    />
    <DevField id="course-title" label="Title">
      <Input
        id="course-title"
        v-model="form.title"
        placeholder="Programmering 1"
        required
      />
    </DevField>
    <DevField id="course-code" label="Code" hint="Unique per school.">
      <Input
        id="course-code"
        v-model="form.code"
        placeholder="PRG1"
        required
      />
    </DevField>
  </DevFormCard>
</template>
