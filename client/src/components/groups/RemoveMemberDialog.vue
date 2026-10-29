<script setup lang="ts">
import {ref, watch} from 'vue'

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
import {api, errorMessage, type Schema} from '@/lib/api'

const props = defineProps<{groupId: string, groupName: string, member: Schema<'GroupMemberDto'> | null}>()
const emit = defineEmits<{removed: [userId: string]}>()

const open = defineModel<boolean>('open', {required: true})

const removing = ref(false)
const error = ref('')

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

async function remove() {
  if (!props.member) {
    return
  }

  removing.value = true
  error.value = ''
  try {
    await api.DELETE('/api/groups/{id}/members/{userId}', {
      params: {path: {id: props.groupId, userId: props.member.userId}},
    })
    emit('removed', props.member.userId)
    open.value = false
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    removing.value = false
  }
}
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
