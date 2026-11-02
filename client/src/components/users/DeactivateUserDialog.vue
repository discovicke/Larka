<script setup lang="ts">
import {computed, watch} from 'vue'

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
import {setUserDeactivated} from '@/lib/usersApi'

type User = Schema<'UserDto'>

const props = defineProps<{user: User}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [user: User]}>()

// Dialogen spärrar en aktiv användare och släpper spärren på en spärrad.
const deactivate = computed(() => !props.user.deactivatedAt)

watch(open, (isOpen) => {
  if (isOpen) {
    error.value = ''
  }
})

const {run: save, loading: saving, error} = useAsync(async () => {
  emit('saved', await setUserDeactivated(props.user.id, deactivate.value))
  open.value = false
}, 'Kunde inte spara. Försök igen.')
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>
            {{ deactivate
              ? `Spärra ${user.name}?`
              : `Återaktivera ${user.name}?` }}
          </DialogTitle>
          <DialogDescription>
            {{ deactivate
              ? 'Hen kan inte logga in längre och syns inte i klasslistor. Inlämningar, betyg och kommentarer finns kvar, och du kan släppa spärren när som helst.'
              : 'Hen kan logga in igen och syns i sina klasser som förut.' }}
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="error" variant="destructive" data-test="deactivate-error">
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
            :variant="deactivate
              ? 'destructive'
              : 'default'"
            :disabled="saving"
            data-test="confirm-deactivate"
          >
            {{ deactivate
              ? 'Spärra'
              : 'Återaktivera' }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
