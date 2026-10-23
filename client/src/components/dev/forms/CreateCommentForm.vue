<script setup lang="ts">
import {reactive} from 'vue'

import {Textarea} from '@/components/ui/textarea'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'

const form = reactive({contentId: '', bodyMd: ''})
const {result, busy, submit: run, fail} = useDevSubmit()

async function submit() {
  if (!form.contentId) {
    fail('Pick the content to comment on.')
    return
  }

  await run(() => api.POST('/api/content/{id}/comments', {
    params: {path: {id: form.contentId}},
    body: {bodyMd: form.bodyMd},
  }))
}
</script>

<template>
  <DevFormCard
    title="Comment"
    description="Written by the placeholder user until we have login."
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="comment-content"
      v-model="form.contentId"
      label="Content"
      source="contents"
    />
    <DevField id="comment-body" label="Comment (markdown)">
      <Textarea
        id="comment-body"
        v-model="form.bodyMd"
        rows="3"
        required
      />
    </DevField>
  </DevFormCard>
</template>
