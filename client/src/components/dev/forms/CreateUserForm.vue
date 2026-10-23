<script setup lang="ts">
import {reactive, ref} from 'vue'

import {Input} from '@/components/ui/input'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {type FormResult, orNull, userRoles} from '../types'

const form = reactive({schoolId: '', name: '', email: '', role: 'Student'})
const result = ref<FormResult | null>(null)

function submit() {
  result.value = {
    kind: 'preview',
    body: {schoolId: orNull(form.schoolId), name: form.name, email: form.email, role: form.role},
  }
}
</script>

<template>
  <DevFormCard
    title="User"
    description="Not sent yet: waiting for the endpoint in #32."
    :result="result"
    @submit="submit"
  >
    <EntitySelect
      id="user-school"
      v-model="form.schoolId"
      label="School"
      source="schools"
    />
    <DevField id="user-name" label="Name">
      <Input id="user-name" v-model="form.name" required/>
    </DevField>
    <DevField id="user-email" label="Email">
      <Input
        id="user-email"
        v-model="form.email"
        type="email"
        required
      />
    </DevField>
    <EnumSelect
      id="user-role"
      v-model="form.role"
      label="Role"
      :values="userRoles"
    />
  </DevFormCard>
</template>
