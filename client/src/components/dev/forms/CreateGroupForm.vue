<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {groupTypes, orNull} from '../types'

const form = reactive({schoolId: '', type: 'Class', name: '', term: ''})
const {result, busy, send} = useDevSubmit()
const {remember} = useDevOptions()

async function submit() {
  const group = await send<{id: string, name: string, term: string}>('POST', '/api/groups', {
    schoolId: orNull(form.schoolId),
    type: form.type,
    name: form.name,
    term: orNull(form.term),
  })
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
      :values="groupTypes"
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
