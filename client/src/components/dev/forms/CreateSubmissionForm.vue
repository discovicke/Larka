<script setup lang="ts">
import {reactive} from 'vue'

import {Textarea} from '@/components/ui/textarea'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {orNull} from '@/lib/form'

const form = reactive({contentLinkId: '', bodyMd: ''})
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember, labelOf} = useDevOptions()

async function submit() {
  if (!form.contentLinkId) {
    fail('Pick the published assignment.')
    return
  }

  const submission = await run(() => api.POST('/api/links/{id}/submissions', {
    params: {path: {id: form.contentLinkId}},
    body: {bodyMd: orNull(form.bodyMd)},
  }))
  if (submission) {
    const link = labelOf('contentLinks', form.contentLinkId) ?? 'Submission'
    remember('submissions', {value: submission.id, label: `${link}: ${submission.status}`})
  }
}
</script>

<template>
  <DevFormCard
    title="Submission"
    description="Handed in by the placeholder user until we have login. Submitting again replaces the earlier one. Files come with the upload endpoints."
    submit-label="Submit"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="submission-link"
      v-model="form.contentLinkId"
      label="Published assignment"
      source="contentLinks"
    />
    <DevField id="submission-body" label="Text (markdown)">
      <Textarea id="submission-body" v-model="form.bodyMd" rows="3"/>
    </DevField>
  </DevFormCard>
</template>
