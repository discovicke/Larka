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
import {unlinkMicrosoft} from '@/lib/usersApi'

type User = Schema<'UserDto'>

const props = defineProps<{user: User}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [user: User]}>()

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: save, loading: saving, error} = useAsync(async () => {
  emit('saved', await unlinkMicrosoft(props.user.id))
  open.value = false
}, 'Kunde inte koppla bort. Försök igen.')
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Koppla bort Microsoft från {{ user.name }}?</DialogTitle>
          <DialogDescription>
            Gör det när skolans IT har skapat om hens Microsoft-konto. Nästa gång hen loggar in kopplas det nya kontot
            på e-postadressen {{ user.email }}. Inget annat ändras.
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="error" variant="destructive" data-test="unlink-error">
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
          <Button type="submit" :disabled="saving" data-test="confirm-unlink">
            Koppla bort
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
