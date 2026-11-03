<script setup lang="ts">
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {type Schema} from '@/lib/api'
import {unlinkMicrosoft} from '@/lib/usersApi'

type User = Schema<'UserDto'>

const props = defineProps<{user: User}>()
const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [user: User]}>()

async function unlink() {
  emit('saved', await unlinkMicrosoft(props.user.id))
}
</script>

<template>
  <ConfirmDialog
    v-model:open="open"
    :title="`Koppla bort Microsoft från ${user.name}?`"
    :description="`Gör det när skolans IT har skapat om hens Microsoft-konto. Nästa gång hen loggar in kopplas det nya kontot på e-postadressen ${user.email}. Inget annat ändras.`"
    confirm-label="Koppla bort"
    confirm-variant="default"
    confirm-test="confirm-unlink"
    error-test="unlink-error"
    failure-text="Kunde inte koppla bort. Försök igen."
    :action="unlink"
  />
</template>
