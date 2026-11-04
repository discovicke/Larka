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
import {Label} from '@/components/ui/label'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useAsync} from '@/composables/useAsync'
import {type Schema} from '@/lib/api'
import {userRoles} from '@/lib/userRoles'
import {updateUserRole} from '@/lib/usersApi'

type User = Schema<'UserDto'>

const props = defineProps<{user: User}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [user: User]}>()

const role = ref<Schema<'UserRole'>>(props.user.role)

watch(open, (isOpen) => {
  if (isOpen) {
    role.value = props.user.role
    error.value = ''
  }
})

const {run: save, loading: saving, error} = useAsync(async () => {
  // Servern säger nej till den egna rollen och till en lärare som fortfarande undervisar. Texten kommer från
  // serverMessages.
  if (role.value !== props.user.role) {
    emit('saved', await updateUserRole(props.user.id, role.value))
  }
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Ändra roll för {{ user.name }}</DialogTitle>
          <DialogDescription>
            Rollen avgör vad hen får göra i skolan. Vilka kurser en lärare undervisar i väljer du på kursen.
          </DialogDescription>
        </DialogHeader>

        <div class="flex flex-col gap-2">
          <Label for="user-role">Roll</Label>
          <NativeSelect id="user-role" v-model="role" data-test="user-role">
            <NativeSelectOption v-for="option in userRoles" :key="option.value" :value="option.value">
              {{ option.label }}
            </NativeSelectOption>
          </NativeSelect>
        </div>

        <p v-if="user.role === 'Teacher' && role !== 'Teacher'" class="text-muted-foreground text-sm" data-test="demote-note">
          Undervisar hen i en kurs eller klass måste du först ersätta hen där. Admin undervisar aldrig.
        </p>

        <Alert v-if="error" variant="destructive" data-test="change-role-error">
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
          <Button type="submit" :disabled="saving" data-test="save-role">
            Spara
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
