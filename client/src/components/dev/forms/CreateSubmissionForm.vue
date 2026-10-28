<script setup lang="ts">
import {reactive, ref} from 'vue'

import {Input} from '@/components/ui/input'
import {Textarea} from '@/components/ui/textarea'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, multipart, type Schema} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'

const form = reactive({contentLinkId: '', bodyMd: ''})
const files = ref<File[]>([])
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember, labelOf} = useDevOptions()

function onFilesChanged(event: Event) {
  files.value = Array.from((event.target as HTMLInputElement).files ?? [])
}

async function submit() {
  if (!form.contentLinkId) {
    fail('Pick the published assignment.')
    return
  }

  const body = new FormData()
  if (form.bodyMd) {
    body.append('bodyMd', form.bodyMd)
  }
  for (const file of files.value) {
    body.append('files', file, file.name)
  }

  // Formuläret skickas som multipart. OpenAPI-typen beskriver en fil som en sträng, så FormData läggs in här.
  const submission = await run(() => api.POST('/api/links/{id}/submissions', {
    params: {path: {id: form.contentLinkId}},
    body: multipart<Schema<'SubmitForm'>>(body),
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
    description="Handed in by the placeholder user until we have login. Submitting again replaces the earlier one, files included."
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
    <DevField id="submission-files" label="Files" hint="Pdf, images, text, Office files or zip. Max 20 MB each.">
      <Input
        id="submission-files"
        type="file"
        multiple
        @change="onFilesChanged"
      />
    </DevField>
  </DevFormCard>
</template>
