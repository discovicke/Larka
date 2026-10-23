<script setup lang="ts">
import {computed, reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {Textarea} from '@/components/ui/textarea'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'
import {contentStatusValues, contentTypeValues, submissionModeValues} from '@/lib/api-types'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EnumSelect from '../EnumSelect.vue'
import {numberOrNull, orNull} from '../types'

const form = reactive({
  type: 'News' as Schema<'ContentType'>,
  title: '',
  bodyMd: '',
  status: 'Published' as Schema<'ContentStatus'>,
  maxScore: '',
  submissionMode: 'None' as Schema<'SubmissionMode'>,
  templateRepo: '',
})
const {result, busy, submit: run} = useDevSubmit()
const {remember} = useDevOptions()

const isAssignment = computed(() => form.type === 'Assignment')
const usesRepo = computed(() => ['Repo', 'FileOrRepo'].includes(form.submissionMode))

// Backend tar bara emot uppgiftsfälten på uppgifter, och templateRepo bara för repo-lägen.
function body(): Schema<'CreateContentDto'> {
  const base = {type: form.type, title: form.title, bodyMd: form.bodyMd, status: form.status}
  if (!isAssignment.value) {
    return base
  }

  return {
    ...base,
    maxScore: numberOrNull(form.maxScore),
    submissionMode: form.submissionMode,
    templateRepo: usesRepo.value
      ? orNull(form.templateRepo)
      : null,
  }
}

async function submit() {
  const content = await run(() => api.POST('/api/content', {body: body()}))
  if (content) {
    remember('contents', {value: content.id, label: `${content.title} (${content.type})`})
  }
}
</script>

<template>
  <DevFormCard
    title="Content"
    description="News, theory or an assignment. The author is the placeholder user until we have login. Publish it to a group or course below."
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EnumSelect
      id="content-type"
      v-model="form.type"
      label="Type"
      :values="contentTypeValues"
    />
    <DevField id="content-title" label="Title">
      <Input id="content-title" v-model="form.title" required/>
    </DevField>
    <DevField id="content-body" label="Body (markdown)">
      <Textarea id="content-body" v-model="form.bodyMd" rows="4"/>
    </DevField>
    <EnumSelect
      id="content-status"
      v-model="form.status"
      label="Status"
      :values="contentStatusValues"
    />

    <template v-if="isAssignment">
      <DevField id="content-max-score" label="Max score" hint="Leave empty for feedback only.">
        <Input
          id="content-max-score"
          v-model="form.maxScore"
          type="number"
          min="1"
        />
      </DevField>
      <EnumSelect
        id="content-submission-mode"
        v-model="form.submissionMode"
        label="Submission mode"
        :values="submissionModeValues"
      />
      <DevField
        v-if="usesRepo"
        id="content-template-repo"
        label="Template repo"
        hint="GitHub org/repo"
      >
        <Input
          id="content-template-repo"
          v-model="form.templateRepo"
          placeholder="org/template"
          required
        />
      </DevField>
    </template>
  </DevFormCard>
</template>
