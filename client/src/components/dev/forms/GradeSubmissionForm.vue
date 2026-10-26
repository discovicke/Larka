<script setup lang="ts">
import {reactive} from 'vue'

import {Checkbox} from '@/components/ui/checkbox'
import {Label} from '@/components/ui/label'
import {Textarea} from '@/components/ui/textarea'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'
import {gradeLevelValues} from '@/lib/api-types'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'

const form = reactive({submissionId: '', level: '', feedback: '', needsWork: false})
const {result, busy, submit: run, fail} = useDevSubmit()

async function submit() {
  if (!form.submissionId) {
    fail('Pick the submission to grade.')
    return
  }

  // Komplettering får bara feedback, inget betyg.
  await run(() => api.POST('/api/submissions/{id}/grade', {
    params: {path: {id: form.submissionId}},
    body: {
      level: form.needsWork || form.level === ''
        ? null
        : form.level as Schema<'GradeLevel'>,
      feedback: form.feedback,
      needsWork: form.needsWork,
    },
  }))
}
</script>

<template>
  <DevFormCard
    title="Grade"
    description="Graded by the placeholder user until we have login."
    submit-label="Grade"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="grade-submission"
      v-model="form.submissionId"
      label="Submission"
      source="submissions"
    />
    <Label for="grade-needs-work">
      <Checkbox
        id="grade-needs-work"
        :model-value="form.needsWork"
        @update:model-value="(value) => (form.needsWork = value === true)"
      />
      Needs work (ask for a resubmission)
    </Label>
    <EnumSelect
      v-if="!form.needsWork"
      id="grade-level"
      v-model="form.level"
      label="Grade (Fail = IG, Pass = G, Distinction = VG)"
      empty-label="No grade, feedback only"
      :values="gradeLevelValues"
    />
    <DevField id="grade-feedback" label="Feedback">
      <Textarea id="grade-feedback" v-model="form.feedback" rows="3"/>
    </DevField>
  </DevFormCard>
</template>
