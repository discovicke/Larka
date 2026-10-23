<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {Textarea} from '@/components/ui/textarea'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {numberOrNull, orNull} from '../types'

const form = reactive({contentLinkId: '', bodyMd: '', fileName: '', fileUrl: '', fileSize: ''})
const {result, busy, send} = useDevSubmit()
const {remember, labelOf} = useDevOptions()

// En fil räcker för testdata. Lämnas tomt skickas inga filer.
function files() {
  if (!form.fileName) {
    return null
  }
  return [{fileName: form.fileName, url: form.fileUrl, sizeBytes: numberOrNull(form.fileSize) ?? 0}]
}

async function submit() {
  if (!form.contentLinkId) {
    result.value = {kind: 'error', message: 'Pick the published assignment.'}
    return
  }

  const submission = await send<{id: string, status: string}>(
    'POST',
    `/api/links/${form.contentLinkId}/submissions`,
    {bodyMd: orNull(form.bodyMd), files: files()},
  )
  if (submission) {
    const link = labelOf('contentLinks', form.contentLinkId) ?? 'Submission'
    remember('submissions', {value: submission.id, label: `${link}: ${submission.status}`})
  }
}
</script>

<template>
  <DevFormCard
    title="Submission"
    description="Handed in by the placeholder user until we have login. Submitting again replaces the earlier one."
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
    <DevField
      id="submission-file-name"
      label="File name"
      hint="Optional. Needed when the assignment takes files."
    >
      <Input id="submission-file-name" v-model="form.fileName" placeholder="solution.zip"/>
    </DevField>
    <template v-if="form.fileName">
      <DevField id="submission-file-url" label="File URL">
        <Input id="submission-file-url" v-model="form.fileUrl" required/>
      </DevField>
      <DevField id="submission-file-size" label="File size (bytes)">
        <Input
          id="submission-file-size"
          v-model="form.fileSize"
          type="number"
          min="0"
        />
      </DevField>
    </template>
  </DevFormCard>
</template>
