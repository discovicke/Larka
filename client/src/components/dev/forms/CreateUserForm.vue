<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {orNull, userRoles} from '../types'

const form = reactive({schoolId: '', name: '', email: '', role: 'Student'})
const {result, busy, send} = useDevSubmit()
const {remember} = useDevOptions()

async function submit() {
  const user = await send<{id: string, name: string, role: string}>('POST', '/api/users', {
    schoolId: orNull(form.schoolId),
    name: form.name,
    email: form.email,
    role: form.role,
  })
  if (user) {
    remember('users', {value: user.id, label: `${user.name} (${user.role})`})
  }
}
</script>

<template>
  <DevFormCard
    title="User"
    description="The email is stored in lowercase and has to be unique."
    :result="result"
    :busy="busy"
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
