<script setup lang="ts">
import {reactive} from 'vue'

import {Checkbox} from '@/components/ui/checkbox'
import {Input} from '@/components/ui/input'
import {Label} from '@/components/ui/label'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import {isoOrNull, orNull} from '../types'

const form = reactive({
  contentId: '',
  groupId: '',
  courseId: '',
  moduleId: '',
  publishAt: '',
  dueAt: '',
  isPinned: false,
})
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember, labelOf} = useDevOptions()

async function submit() {
  if (!form.contentId) {
    fail('Pick the content to publish.')
    return
  }
  if (!form.groupId && !form.courseId) {
    fail('Pick a group, a course or both.')
    return
  }

  const link = await run(() => api.POST('/api/content/{id}/links', {
    params: {path: {id: form.contentId}},
    body: {
      groupId: orNull(form.groupId),
      courseId: orNull(form.courseId),
      moduleId: orNull(form.moduleId),
      publishAt: isoOrNull(form.publishAt),
      dueAt: isoOrNull(form.dueAt),
      isPinned: form.isPinned,
    },
  }))

  // Utdelade uppgifter går att lämna in på, så de ska synas i inlämningsformuläret direkt.
  const content = labelOf('contents', form.contentId)
  if (link && content?.endsWith('(Assignment)')) {
    const target = labelOf('groups', form.groupId) ?? labelOf('courses', form.courseId)
    remember('contentLinks', {value: link.id, label: `${content.replace(' (Assignment)', '')} → ${target}`})
  }
}
</script>

<template>
  <DevFormCard
    title="Publish content"
    description="ContentLink. Group only, course only (all groups reading it), or both."
    submit-label="Publish"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="publish-content"
      v-model="form.contentId"
      label="Content"
      source="contents"
    />
    <EntitySelect
      id="publish-group"
      v-model="form.groupId"
      label="Group"
      source="groups"
      optional
    />
    <EntitySelect
      id="publish-course"
      v-model="form.courseId"
      label="Course"
      source="courses"
      optional
    />
    <EntitySelect
      id="publish-module"
      v-model="form.moduleId"
      label="Module"
      source="modules"
      optional
    />
    <DevField id="publish-at" label="Publish at" hint="Leave empty for now.">
      <Input id="publish-at" v-model="form.publishAt" type="datetime-local"/>
    </DevField>
    <DevField id="publish-due" label="Due at" hint="Only for assignments.">
      <Input id="publish-due" v-model="form.dueAt" type="datetime-local"/>
    </DevField>
    <Label for="publish-pinned">
      <Checkbox
        id="publish-pinned"
        :model-value="form.isPinned"
        @update:model-value="(value) => (form.isPinned = value === true)"
      />
      Pinned
    </Label>
  </DevFormCard>
</template>
