<script setup lang="ts">
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {type Schema} from '@/lib/api'
import {removeGroupMember} from '@/lib/groupsApi'

const props = defineProps<{groupId: string, groupName: string, member: Schema<'GroupMemberDto'> | null}>()
const emit = defineEmits<{removed: [userId: string]}>()
const open = defineModel<boolean>('open', {required: true})

async function remove() {
  if (props.member) {
    await removeGroupMember(props.groupId, props.member.userId)
  }
}
</script>

<template>
  <ConfirmDialog
    v-model:open="open"
    :title="`Ta bort ${member?.name ?? ''} från ${groupName}?`"
    description="Hen förlorar åtkomsten som kommer med gruppen."
    confirm-label="Ta bort"
    confirm-test="confirm-remove-member"
    :action="remove"
    @done="member && emit('removed', member.userId)"
  />
</template>
