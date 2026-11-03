<script setup lang="ts">
import {computed} from 'vue'

import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {type Schema} from '@/lib/api'
import {setUserDeactivated} from '@/lib/usersApi'

type User = Schema<'UserDto'>

const props = defineProps<{user: User}>()
const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [user: User]}>()

// Dialogen spärrar en aktiv användare och släpper spärren på en spärrad.
const deactivate = computed(() => !props.user.deactivatedAt)

async function save() {
  emit('saved', await setUserDeactivated(props.user.id, deactivate.value))
}
</script>

<template>
  <ConfirmDialog
    v-model:open="open"
    :title="deactivate
      ? `Spärra ${user.name}?`
      : `Återaktivera ${user.name}?`"
    :description="deactivate
      ? 'Hen kan inte logga in längre och syns inte i klasslistor. Inlämningar, betyg och kommentarer finns kvar, och du kan släppa spärren när som helst.'
      : 'Hen kan logga in igen och syns i sina klasser som förut.'"
    :confirm-label="deactivate
      ? 'Spärra'
      : 'Återaktivera'"
    :confirm-variant="deactivate
      ? 'destructive'
      : 'default'"
    confirm-test="confirm-deactivate"
    error-test="deactivate-error"
    failure-text="Kunde inte spara. Försök igen."
    :action="save"
  />
</template>
