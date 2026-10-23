<script setup lang="ts">
import {reactive} from 'vue'

import {useDevSubmit} from '@/composables/useDevSubmit'
import {api, type Schema} from '@/lib/api'
import {groupRoleValues} from '@/lib/api-types'

import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'

const form = reactive({groupId: '', userId: '', role: 'Student' as Schema<'GroupRole'>})
const {result, busy, submit: run, fail} = useDevSubmit()

async function submit() {
  if (!form.groupId || !form.userId) {
    fail('Pick a group and a user.')
    return
  }

  await run(() => api.POST('/api/groups/{id}/members', {
    params: {path: {id: form.groupId}},
    body: {userId: form.userId, role: form.role},
  }))
}
</script>

<template>
  <DevFormCard
    title="Add user to group"
    description="Enrollment. The role only applies in this group, and the user has to be at the group's school."
    submit-label="Add"
    :result="result"
    :busy="busy"
    @submit="submit"
  >
    <EntitySelect
      id="member-group"
      v-model="form.groupId"
      label="Group"
      source="groups"
    />
    <EntitySelect
      id="member-user"
      v-model="form.userId"
      label="User"
      source="users"
    />
    <EnumSelect
      id="member-role"
      v-model="form.role"
      label="Role in group"
      :values="groupRoleValues"
    />
  </DevFormCard>
</template>
