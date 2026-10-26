<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'
import {groupTypeValues} from '@/lib/api-types'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {orNull} from '@/lib/form'

const form = reactive({schoolId: '', type: 'Class' as Schema<'GroupType'>, name: '', term: ''})
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember} = useDevOptions()

async function submit() {
  if (!form.schoolId) {
    fail('Pick a school.')
    return
  }

  const group = await run(() => api.POST('/api/groups', {
    body: {schoolId: form.schoolId, type: form.type, name: form.name, term: orNull(form.term)},
  }))
  if (group) {
    remember('groups', {value: group.id, label: `${group.name} (${group.term})`})
  }
}
</script>

<template>
  <DevFormCard
    title="Group"
    description="Name and term have to be unique per school."
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="group-school"
      v-model="form.schoolId"
      label="School"
      source="schools"
    />
    <EnumSelect
      id="group-type"
      v-model="form.type"
      label="Type"
      :values="groupTypeValues"
    />
    <DevField id="group-name" label="Name">
      <Input
        id="group-name"
        v-model="form.name"
        placeholder="NET25"
        required
      />
    </DevField>
    <DevField id="group-term" label="Term">
      <Input id="group-term" v-model="form.term" placeholder="HT25"/>
    </DevField>
  </DevFormCard>
</template>
