<script setup lang="ts">
import ConfirmDialog from '@/components/ConfirmDialog.vue'
import {api, type Schema} from '@/lib/api'

const props = defineProps<{group: Schema<'GroupDto'>}>()
const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{deleted: [group: Schema<'GroupDto'>]}>()

function remove() {
  return api.DELETE('/api/groups/{id}', {params: {path: {id: props.group.id}}}).then(() => undefined)
}
</script>

<template>
  <ConfirmDialog
    v-model:open="open"
    :title="`Ta bort ${group.name}?`"
    description="Gruppens medlemskap och kurskopplingar tas bort. Användarna och kurserna finns kvar. Det går inte att ångra."
    confirm-label="Ta bort gruppen"
    confirm-test="confirm-delete-group"
    error-test="delete-group-error"
    conflict-message="Gruppen har publicerat innehåll och kan inte tas bort."
    :action="remove"
    @done="emit('deleted', group)"
  />
</template>
