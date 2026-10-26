<script setup lang="ts">
import {reactive, ref} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'

const form = reactive({contentId: ''})
const file = ref<File | null>(null)
const {result, busy, submit: run, fail} = useDevSubmit()

function onFileChanged(event: Event) {
  file.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

async function submit() {
  if (!form.contentId) {
    fail('Pick the content to attach the file to.')
    return
  }
  if (!file.value) {
    fail('Pick a file.')
    return
  }

  const body = new FormData()
  body.append('file', file.value, file.value.name)

  // Formuläret skickas som multipart. OpenAPI-typen beskriver filen som en sträng, så FormData läggs in här.
  await run(() => api.POST('/api/content/{id}/attachments', {
    params: {path: {id: form.contentId}},
    body: body as unknown as {file: Schema<'IFormFile'>},
  }))
}
</script>

<template>
  <DevFormCard
    title="Attachment"
    description="Adds a file to a content item. Students get a link to it that works for 10 minutes."
    submit-label="Upload"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="attachment-content"
      v-model="form.contentId"
      label="Content"
      source="contents"
    />
    <DevField id="attachment-file" label="File" hint="Pdf, images, text, Office files or zip. Max 20 MB.">
      <Input id="attachment-file" type="file" @change="onFileChanged"/>
    </DevField>
  </DevFormCard>
</template>
