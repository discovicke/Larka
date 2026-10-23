<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api} from '@/lib/api'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'

const form = reactive({name: ''})
const {result, busy, submit: run} = useDevSubmit()
const {remember} = useDevOptions()

async function submit() {
  const school = await run(() => api.POST('/api/schools', {body: {name: form.name}}))
  if (school) {
    remember('schools', {value: school.id, label: school.name})
  }
}
</script>

<template>
  <DevFormCard
    title="School"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <DevField id="school-name" label="Name">
      <Input id="school-name" v-model="form.name" required/>
    </DevField>
  </DevFormCard>
</template>
