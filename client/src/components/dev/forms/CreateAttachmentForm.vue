<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevSubmit} from '@/composables/useDevSubmit'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {numberOrNull} from '../types'

const form = reactive({contentId: '', fileName: '', url: '', sizeBytes: ''})
const {result, busy, send} = useDevSubmit()

async function submit() {
  if (!form.contentId) {
    result.value = {kind: 'error', message: 'Pick the content to attach to.'}
    return
  }

  await send('POST', `/api/content/${form.contentId}/attachments`, {
    fileName: form.fileName,
    url: form.url,
    sizeBytes: numberOrNull(form.sizeBytes),
  })
}
</script>

<template>
  <DevFormCard
    title="Attachment"
    description="Only the metadata. No real file is uploaded."
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
    <DevField id="attachment-file-name" label="File name">
      <Input
        id="attachment-file-name"
        v-model="form.fileName"
        placeholder="lab1.pdf"
        required
      />
    </DevField>
    <DevField id="attachment-url" label="URL">
      <Input
        id="attachment-url"
        v-model="form.url"
        placeholder="https://example.com/lab1.pdf"
        required
      />
    </DevField>
    <DevField id="attachment-size" label="Size (bytes)">
      <Input
        id="attachment-size"
        v-model="form.sizeBytes"
        type="number"
        min="0"
        required
      />
    </DevField>
  </DevFormCard>
</template>
