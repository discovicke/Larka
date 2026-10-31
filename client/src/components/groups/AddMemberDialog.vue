<script setup lang="ts">
import {computed, reactive, ref, watch} from 'vue'

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
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useAsync} from '@/composables/useAsync'
import {api, errorMessage, unwrap, type Schema} from '@/lib/api'
import {groupRoles} from '@/lib/groupRoles'
import {addGroupMember} from '@/lib/groupsApi'
import {fetchUsers} from '@/lib/usersApi'

const props = defineProps<{groupId: string, groupName: string, schoolId: string, memberIds: string[]}>()
const emit = defineEmits<{added: [member: Schema<'GroupMemberDto'>], paste: []}>()

const open = defineModel<boolean>('open', {required: true})

const users = ref<Schema<'UserDto'>[]>([])
const search = ref('')
const userId = ref('')
const role = ref<Schema<'GroupRole'>>('Student')
const saving = ref(false)
// Samma felruta visar felet från hämtningen och från sparandet.
const {run: loadUsers, loading, error} = useAsync(fetchUsers)

// Fliken Skapa användare: en ny användare skapas och läggs till i gruppen i samma steg.
const creating = ref(false)
const submitted = ref(false)
const form = reactive({name: '', email: '', schoolRole: 'Student' as Schema<'UserRole'>, password: ''})
// Har användaren skapats men inte kunnat läggas till, används samma användare vid nästa försök.
let createdUser: Schema<'UserDto'> | null = null

const schoolRoles: {value: Schema<'UserRole'>, label: string}[] = [
  {value: 'Student', label: 'Elev'},
  {value: 'Teacher', label: 'Lärare'},
]

const nameError = computed(() => submitted.value && !form.name.trim()
  ? 'Ange ett namn.'
  : '')
const emailError = computed(() => submitted.value && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())
  ? 'Ange en giltig e-postadress.'
  : '')
const passwordError = computed(() => form.password !== '' && form.password.length < 8
  ? 'Lösenordet måste vara minst 8 tecken.'
  : '')

// Den som redan är medlem kan inte läggas till igen.
const candidates = computed(() => {
  const query = search.value.trim().toLowerCase()
  return users.value
    .filter((user) => !props.memberIds.includes(user.id))
    .filter((user) => query === ''
      || user.name.toLowerCase().includes(query)
      || user.email.toLowerCase().includes(query))
})

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  search.value = ''
  userId.value = ''
  role.value = 'Student'
  creating.value = false
  submitted.value = false
  createdUser = null
  Object.assign(form, {name: '', email: '', schoolRole: 'Student', password: ''})
  const loaded = await loadUsers()
  if (loaded) {
    users.value = loaded
  }
})

// Ett val som filtret döljer ska inte skickas.
watch(candidates, (list) => {
  if (!list.some((user) => user.id === userId.value)) {
    userId.value = ''
  }
})

function showCreate() {
  error.value = ''
  creating.value = true
}

function showPick() {
  error.value = ''
  creating.value = false
}

// Svaren från API:et är på engelska, så de vanligaste översätts.
function createError(exception: unknown): string {
  const message = errorMessage(exception)
  return message.includes('email is already in use')
    ? 'E-postadressen används redan av en annan användare.'
    : message
}

async function submit() {
  submitted.value = true
  if (creating.value && (!form.name.trim() || emailError.value || passwordError.value)) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    if (creating.value) {
      createdUser ??= unwrap(await api.POST('/api/users', {
        body: {
          schoolId: props.schoolId,
          name: form.name.trim(),
          email: form.email.trim(),
          role: form.schoolRole,
          password: form.password || null,
        },
      }))
    }
    await addToGroup(creating.value
      ? createdUser
      : users.value.find((found) => found.id === userId.value))
  } catch (exception) {
    error.value = creating.value && createdUser
      ? `Användaren skapades men kunde inte läggas till i gruppen: ${errorMessage(exception)}`
      : createError(exception)
  } finally {
    saving.value = false
  }
}

async function addToGroup(user: Schema<'UserDto'> | null | undefined) {
  if (!user) {
    return
  }

  await addGroupMember(props.groupId, user.id, role.value)
  emit('added', {userId: user.id, name: user.name, role: role.value})
  open.value = false
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" novalidate @submit.prevent="submit">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Lägg till medlem i {{ groupName }}</DialogTitle>
          <DialogDescription>
            {{ creating
              ? 'Skapa en ny användare och lägg till hen i gruppen.'
              : 'Välj en användare och vilken roll hen har i gruppen.' }}
          </DialogDescription>
        </DialogHeader>

        <template v-if="creating">
          <Field
            :data-invalid="nameError
              ? true
              : undefined"
          >
            <FieldLabel for="new-user-name">Namn</FieldLabel>
            <Input
              id="new-user-name"
              v-model="form.name"
              class="w-full"
              autocomplete="off"
              :aria-invalid="nameError
                ? true
                : undefined"
              data-test="new-user-name"
            />
            <FieldError v-if="nameError" data-test="new-user-name-error">{{ nameError }}</FieldError>
          </Field>
          <Field
            :data-invalid="emailError
              ? true
              : undefined"
          >
            <FieldLabel for="new-user-email">E-post</FieldLabel>
            <Input
              id="new-user-email"
              v-model="form.email"
              type="email"
              class="w-full"
              autocomplete="off"
              :aria-invalid="emailError
                ? true
                : undefined"
              data-test="new-user-email"
            />
            <FieldError v-if="emailError" data-test="new-user-email-error">{{ emailError }}</FieldError>
          </Field>
          <Field>
            <FieldLabel for="new-user-role">Roll i skolan</FieldLabel>
            <NativeSelect
              id="new-user-role"
              v-model="form.schoolRole"
              class="w-full"
              data-test="new-user-role"
            >
              <NativeSelectOption v-for="option in schoolRoles" :key="option.value" :value="option.value">
                {{ option.label }}
              </NativeSelectOption>
            </NativeSelect>
          </Field>
          <Field
            :data-invalid="passwordError
              ? true
              : undefined"
          >
            <FieldLabel for="new-user-password">Tillfälligt lösenord (valfritt)</FieldLabel>
            <Input
              id="new-user-password"
              v-model="form.password"
              type="text"
              class="w-full"
              autocomplete="off"
              :aria-invalid="passwordError
                ? true
                : undefined"
              data-test="new-user-password"
            />
            <FieldDescription>Minst 8 tecken. Utan lösenord kan användaren inte logga in ännu.</FieldDescription>
            <FieldError v-if="passwordError" data-test="new-user-password-error">{{ passwordError }}</FieldError>
          </Field>
        </template>

        <template v-else>
          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="member-search">Sök användare</FieldLabel>
            <Input
              id="member-search"
              v-model="search"
              class="w-full"
              placeholder="Namn eller e-post"
              :disabled="loading"
              data-test="member-search"
            />
          </div>

          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="member-user">Användare</FieldLabel>
            <NativeSelect
              id="member-user"
              v-model="userId"
              class="w-full"
              required
              :disabled="loading"
              data-test="member-user"
            >
              <NativeSelectOption value="">
                {{ candidates.length === 0 && !loading
                  ? 'Inga användare hittades'
                  : 'Välj användare…' }}
              </NativeSelectOption>
              <NativeSelectOption v-for="user in candidates" :key="user.id" :value="user.id">
                {{ user.name }} · {{ user.email }}
              </NativeSelectOption>
            </NativeSelect>
          </div>

          <p class="text-muted-foreground text-sm">
            Finns inte i listan?
            <Button
              type="button"
              variant="link"
              class="h-auto p-0"
              data-test="show-create-user"
              @click="showCreate"
            >
              Skapa användare
            </Button>
            eller
            <Button
              type="button"
              variant="link"
              class="h-auto p-0"
              data-test="show-paste"
              @click="emit('paste')"
            >
              klistra in en lista med elever
            </Button>.
          </p>
        </template>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="member-role">Roll</FieldLabel>
          <NativeSelect
            id="member-role"
            v-model="role"
            class="w-full"
            data-test="member-role-select"
          >
            <NativeSelectOption v-for="option in groupRoles" :key="option.value" :value="option.value">
              {{ option.label }}
            </NativeSelectOption>
          </NativeSelect>
        </div>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            v-if="creating"
            type="button"
            variant="ghost"
            :disabled="saving"
            data-test="show-pick-user"
            @click="showPick"
          >
            Tillbaka till listan
          </Button>
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
            :disabled="saving || (!creating && userId === '')"
            data-test="confirm-add-member"
          >
            {{ creating
              ? 'Skapa och lägg till'
              : 'Lägg till' }}
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
