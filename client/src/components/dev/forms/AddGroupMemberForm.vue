<script setup lang="ts">
import {reactive} from 'vue'

import {useDevSubmit} from '@/composables/useDevSubmit'

import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {groupRoles, orNull} from '../types'

const form = reactive({groupId: '', userId: '', role: 'Student'})
const {result, busy, send} = useDevSubmit()

async function submit() {
  if (!form.groupId) {
    result.value = {kind: 'error', message: 'Pick a group.'}
    return
  }

  await send('POST', `/api/groups/${form.groupId}/members`, {userId: orNull(form.userId), role: form.role})
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
      :values="groupRoles"
    />
  </DevFormCard>
</template>
