<script setup lang="ts">
import {computed, watch} from 'vue'

import CreateUserFields from '@/components/users/CreateUserFields.vue'
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
import {useCreateUserForm} from '@/composables/useCreateUserForm'
import {type Schema} from '@/lib/api'
import {userRoles} from '@/lib/userRoles'

type UserRole = Schema<'UserRole'>

// Skapar en användare i skolan, utan grupp. Sidan Användare erbjuder alla roller. Ny kurs öppnar dialogen
// med bara Lärare, så att admin kan skapa läraren direkt.
const props = withDefaults(defineProps<{schoolId: string, roles?: UserRole[]}>(), {
  roles: () => userRoles.map((option) => option.value),
})
const emit = defineEmits<{created: [user: Schema<'UserDto'>]}>()

const open = defineModel<boolean>('open', {required: true})

const newUser = useCreateUserForm(props.roles[0])
const onlyTeachers = computed(() => props.roles.length === 1 && props.roles[0] === 'Teacher')

const {run: create, loading: saving, error} = useAsync(async () => {
  if (!newUser.validate()) {
    return
  }
  emit('created', await newUser.create(props.schoolId))
  open.value = false
})

watch(open, (isOpen) => {
  if (isOpen) {
    newUser.reset()
    error.value = ''
  }
}, {immediate: true})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" novalidate @submit.prevent="create">
        <DialogHeader class="min-w-0">
          <DialogTitle>
            {{ onlyTeachers
              ? 'Ny lärare'
              : 'Ny användare' }}
          </DialogTitle>
          <DialogDescription>
            {{ onlyTeachers
              ? 'Läraren skapas i skolan och kan sedan undervisa i kurser.'
              : 'Användaren skapas i skolan. Vilka grupper och kurser hen är med i väljer du där.' }}
          </DialogDescription>
        </DialogHeader>

        <CreateUserFields :state="newUser" :roles="roles"/>

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
          <Button type="submit" :disabled="saving" data-test="confirm-create-user">
            {{ onlyTeachers
              ? 'Skapa lärare'
              : 'Skapa användare' }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
