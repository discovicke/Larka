<script setup lang="ts">
import {watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {useAsync} from '@/composables/useAsync'
import {type Schema} from '@/lib/api'
import {removeGroupMember} from '@/lib/groupsApi'

const props = defineProps<{groupId: string, groupName: string, member: Schema<'GroupMemberDto'> | null}>()
const emit = defineEmits<{removed: [userId: string]}>()

const open = defineModel<boolean>('open', {required: true})

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: remove, loading: removing, error} = useAsync(async () => {
  if (!props.member) {
    return
  }

  await removeGroupMember(props.groupId, props.member.userId)
  emit('removed', props.member.userId)
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-md">
      <DialogHeader class="min-w-0">
        <DialogTitle class="break-words">Ta bort {{ member?.name }} från {{ groupName }}?</DialogTitle>
        <DialogDescription>Hen förlorar åtkomsten som kommer med gruppen.</DialogDescription>
      </DialogHeader>

      <Alert v-if="error" variant="destructive">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>

      <DialogFooter>
        <Button
          type="button"
          variant="outline"
          :disabled="removing"
          @click="open = false"
        >
          Avbryt
        </Button>
        <Button
          type="button"
          variant="destructive"
          :disabled="removing"
          data-test="confirm-remove-member"
          @click="remove"
        >
          Ta bort
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
