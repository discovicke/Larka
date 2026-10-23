<script setup lang="ts">
import {reactive, ref} from 'vue'

import {Input} from '@/components/ui/input'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {type FormResult, groupTypes, orNull} from '../types'

const form = reactive({schoolId: '', type: 'Class', name: '', term: ''})
const result = ref<FormResult | null>(null)

function submit() {
  result.value = {
    kind: 'preview',
    body: {schoolId: orNull(form.schoolId), type: form.type, name: form.name, term: form.term},
  }
}
</script>

<template>
  <DevFormCard
    title="Group"
    description="Not sent yet: waiting for the endpoint in #32."
    :result="result"
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
      <Input
        id="group-term"
        v-model="form.term"
        placeholder="HT25"
        required
      />
    </DevField>
  </DevFormCard>
</template>
