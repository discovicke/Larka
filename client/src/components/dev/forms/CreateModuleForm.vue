<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {orNull} from '@/lib/form'

const form = reactive({courseId: '', title: '', description: ''})
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember, labelOf} = useDevOptions()

async function submit() {
  if (!form.courseId) {
    fail('Pick a course.')
    return
  }

  const module = await run(() => api.POST('/api/courses/{id}/modules', {
    params: {path: {id: form.courseId}},
    body: {title: form.title, description: orNull(form.description)},
  }))
  if (module) {
    const course = labelOf('courses', form.courseId)?.split(' ')[0]
    remember('modules', {value: module.id, label: `${course}: ${module.title}`})
  }
}
</script>

<template>
  <DevFormCard
    title="Module"
    description="A week in a course. Added last, pick it when publishing content."
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="module-course"
      v-model="form.courseId"
      label="Course"
      source="courses"
    />
    <DevField id="module-title" label="Title">
      <Input
        id="module-title"
        v-model="form.title"
        placeholder="Vecka 1 – CI/CD"
        required
      />
    </DevField>
    <DevField id="module-description" label="Description">
      <Input id="module-description" v-model="form.description"/>
    </DevField>
  </DevFormCard>
</template>
