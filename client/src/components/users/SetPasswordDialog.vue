<script setup lang="ts">
import {computed, ref, watch} from 'vue'

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
import {Field, FieldDescription, FieldError, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {useAsync} from '@/composables/useAsync'
import {useToast} from '@/composables/useToast'
import {type Schema} from '@/lib/api'
import {setUserPassword} from '@/lib/usersApi'

type User = Schema<'UserDto'>

// Admin sätter ett tillfälligt lösenord, till exempel för en elev som skapades utan lösenord eller har
// glömt sitt. Lösenordet syns i fältet, så att admin kan läsa upp det eller skriva av det.
const props = defineProps<{user: Pick<User, 'id' | 'name'>}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [user: User]}>()

const minLength = 8
const password = ref('')
const submitted = ref(false)

const passwordError = computed(() => submitted.value && password.value.length < minLength
  ? `Lösenordet måste vara minst ${minLength} tecken.`
  : '')

watch(open, (isOpen) => {
  if (isOpen) {
    password.value = ''
    submitted.value = false
    error.value = ''
  }
})

const {run: save, loading: saving, error} = useAsync(async () => {
  submitted.value = true
  if (passwordError.value) {
    return
  }
  // Servern säger nej till det egna lösenordet och till den som loggar in med Microsoft. Texten kommer från
  // serverMessages.
  emit('saved', await setUserPassword(props.user.id, password.value))
  useToast().showToast(`Lösenordet är sparat. Ge det till ${props.user.name}.`)
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent>
      <form class="flex flex-col gap-4" novalidate @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Sätt lösenord för {{ user.name }}</DialogTitle>
          <DialogDescription>
            Hen loggar in med e-postadressen och lösenordet. Ett lösenord som hen redan har slutar gälla.
          </DialogDescription>
        </DialogHeader>

        <Field
          :data-invalid="passwordError
            ? true
            : undefined"
        >
          <FieldLabel for="set-password">Tillfälligt lösenord</FieldLabel>
          <Input
            id="set-password"
            v-model="password"
            type="text"
            class="w-full"
            autocomplete="off"
            :aria-invalid="passwordError
              ? true
              : undefined"
            data-test="set-password"
          />
          <FieldDescription>Minst {{ minLength }} tecken.</FieldDescription>
          <FieldError v-if="passwordError" data-test="set-password-error">{{ passwordError }}</FieldError>
        </Field>

        <Alert v-if="error" variant="destructive" data-test="set-password-server-error">
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
          <Button type="submit" :disabled="saving" data-test="save-password">
            Spara
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
