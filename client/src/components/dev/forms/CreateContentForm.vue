<script setup lang="ts">
import {computed, reactive} from 'vue'

import {Checkbox} from '@/components/ui/checkbox'
import {Input} from '@/components/ui/input'
import {Label} from '@/components/ui/label'
import {Textarea} from '@/components/ui/textarea'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'
import {
  contentStatusValues,
  contentTypeValues,
  gradeScaleValues,
  submissionModeValues,
} from '@/lib/api-types'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EnumSelect from '../EnumSelect.vue'
import {orNull} from '@/lib/form'

const form = reactive({
  type: 'News' as Schema<'ContentType'>,
  title: '',
  bodyMd: '',
  status: 'Published' as Schema<'ContentStatus'>,
  gradeScale: 'None' as Schema<'GradeScale'>,
  submissionModes: [] as Schema<'SubmissionMode'>[],
  templateRepo: '',
})
const {result, busy, submit: run} = useDevSubmit()
const {remember} = useDevOptions()

const isAssignment = computed(() => form.type === 'Assignment')
const usesRepo = computed(() => form.submissionModes.includes('Repo'))

function toggleMode(mode: Schema<'SubmissionMode'>, checked: boolean) {
  const others = form.submissionModes.filter((selected) => selected !== mode)
  form.submissionModes = checked
    ? [...others, mode]
    : others
}

// Backend tar bara emot uppgiftsfälten på uppgifter, och templateRepo bara när GitHub är ett sätt att lämna in.
function body(): Schema<'CreateContentDto'> {
  const base = {type: form.type, title: form.title, bodyMd: form.bodyMd, status: form.status}
  if (!isAssignment.value) {
    return base
  }

  return {
    ...base,
    gradeScale: form.gradeScale,
    submissionModes: form.submissionModes,
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
      <EnumSelect
        id="content-grade-scale"
        v-model="form.gradeScale"
        label="Grade scale (None = exercise, PassFail = IG-G, PassFailDistinction = IG-G-VG)"
        :values="gradeScaleValues"
      />
      <DevField id="content-modes" label="How students hand in" hint="Pick none, one or several.">
        <Label
          v-for="mode in submissionModeValues"
          :key="mode"
          :for="`content-mode-${mode}`"
        >
          <Checkbox
            :id="`content-mode-${mode}`"
            :model-value="form.submissionModes.includes(mode)"
            @update:model-value="(value) => toggleMode(mode, value === true)"
          />
          {{ mode }}
        </Label>
      </DevField>
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
