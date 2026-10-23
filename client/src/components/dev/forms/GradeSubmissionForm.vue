<script setup lang="ts">
import {reactive} from 'vue'

import {Checkbox} from '@/components/ui/checkbox'
import {Input} from '@/components/ui/input'
import {Label} from '@/components/ui/label'
import {Textarea} from '@/components/ui/textarea'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {numberOrNull} from '../types'

const form = reactive({submissionId: '', score: '', feedback: '', needsWork: false})
const {result, busy, submit: run, fail} = useDevSubmit()

async function submit() {
  if (!form.submissionId) {
    fail('Pick the submission to grade.')
    return
  }

  // Komplettering får bara feedback, ingen poäng.
  await run(() => api.POST('/api/submissions/{id}/grade', {
    params: {path: {id: form.submissionId}},
    body: {
      score: form.needsWork
        ? null
        : numberOrNull(form.score),
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
    <DevField
      v-if="!form.needsWork"
      id="grade-score"
      label="Score"
      hint="Leave empty if the assignment has no score."
    >
      <Input
        id="grade-score"
        v-model="form.score"
        type="number"
        min="0"
      />
    </DevField>
    <DevField id="grade-feedback" label="Feedback">
      <Textarea id="grade-feedback" v-model="form.feedback" rows="3"/>
    </DevField>
  </DevFormCard>
</template>
