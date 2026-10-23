<script setup lang="ts">
import {reactive, ref} from 'vue'

import {Input} from '@/components/ui/input'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {type FormResult, orNull} from '../types'

const form = reactive({schoolId: '', title: '', code: ''})
const result = ref<FormResult | null>(null)

function submit() {
  result.value = {
    kind: 'preview',
    body: {schoolId: orNull(form.schoolId), title: form.title, code: form.code},
  }
}
</script>

<template>
  <DevFormCard
    title="Course"
    description="Not sent yet: waiting for the endpoint in #32."
    :result="result"
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
