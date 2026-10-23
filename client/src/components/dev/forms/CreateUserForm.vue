<script setup lang="ts">
import {reactive} from 'vue'

import {Input} from '@/components/ui/input'
import {useDevOptions} from '@/composables/useDevOptions'
import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'
import {userRoleValues} from '@/lib/api-types'

import DevField from '../DevField.vue'
import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'

const form = reactive({schoolId: '', name: '', email: '', role: 'Student' as Schema<'UserRole'>})
const {result, busy, submit: run, fail} = useDevSubmit()
const {remember} = useDevOptions()

async function submit() {
  if (!form.schoolId) {
    fail('Pick a school.')
    return
  }

  const user = await run(() => api.POST('/api/users', {
    body: {schoolId: form.schoolId, name: form.name, email: form.email, role: form.role},
  }))
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
      :values="userRoleValues"
    />
  </DevFormCard>
</template>
