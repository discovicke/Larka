<script setup lang="ts">
import {reactive, ref} from 'vue'

import DevFormCard from '../DevFormCard.vue'
import EntitySelect from '../EntitySelect.vue'
import EnumSelect from '../EnumSelect.vue'
import {type FormResult, groupRoles, orNull} from '../types'

const form = reactive({groupId: '', userId: '', role: 'Student'})
const result = ref<FormResult | null>(null)

function submit() {
  result.value = {
    kind: 'preview',
    body: {groupId: orNull(form.groupId), userId: orNull(form.userId), role: form.role},
  }
}
</script>

<template>
  <DevFormCard
    title="Add user to group"
    description="Enrollment. The role only applies in this group. Not sent yet: there is no endpoint for it."
    submit-label="Add"
    :result="result"
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
