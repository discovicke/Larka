<script setup lang="ts">
import {reactive, ref} from 'vue'

import {Input} from '@/components/ui/input'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import type {FormResult} from '../types'

const form = reactive({name: ''})
const result = ref<FormResult | null>(null)

function submit() {
  result.value = {kind: 'preview', body: {name: form.name}}
}
</script>

<template>
  <DevFormCard
    title="School"
    description="Not sent yet: waiting for the endpoint in #32."
    :result="result"
    @submit="submit"
  >
    <DevField id="school-name" label="Name">
      <Input id="school-name" v-model="form.name" required/>
    </DevField>
  </DevFormCard>
</template>
