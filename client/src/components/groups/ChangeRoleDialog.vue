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
import {FieldLabel} from '@/components/ui/field'
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select'
import {errorMessage, type Schema} from '@/lib/api'
import {groupRoles} from '@/lib/groupRoles'
import {changeMemberRole} from '@/lib/groupsApi'

const props = defineProps<{
  groupId: string
  groupName: string
  member: Schema<'GroupMemberDto'> | null
}>()
const emit = defineEmits<{changed: []}>()

const open = defineModel<boolean>('open', {required: true})

const role = ref<Schema<'GroupRole'>>('Student')
const saving = ref(false)
const error = ref('')

watch(open, (isOpen) => {
  if (isOpen && props.member) {
    role.value = props.member.role
    error.value = ''
  }
})

async function save() {
  if (!props.member) {
    return
  }

  const member = props.member
  saving.value = true
  error.value = ''
  try {
    await changeMemberRole(props.groupId, member.userId, role.value)
    emit('changed')
    open.value = false
  } catch (exception) {
    error.value = `Kunde inte ändra rollen för ${member.name}. ${errorMessage(exception)}`
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-md">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="save">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Ändra roll för {{ member?.name }}</DialogTitle>
          <DialogDescription>Rollen gäller i {{ groupName }}.</DialogDescription>
        </DialogHeader>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="change-role">Roll</FieldLabel>
          <Select v-model="role" :disabled="saving">
            <SelectTrigger id="change-role" class="w-full" data-test="change-role-select">
              <SelectValue/>
            </SelectTrigger>
            <SelectContent position="popper">
              <SelectItem v-for="option in groupRoles" :key="option.value" :value="option.value">
                {{ option.label }}
              </SelectItem>
            </SelectContent>
          </Select>
        </div>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button
            type="submit"
            :disabled="saving || role === member?.role"
            data-test="confirm-change-role"
          >
            Spara
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
