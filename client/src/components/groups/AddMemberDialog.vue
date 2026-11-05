<script setup lang="ts">
import {computed, ref, watch} from 'vue'

import SearchPicker from '@/components/SearchPicker.vue'
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
import {FieldLabel} from '@/components/ui/field'
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select'
import {useAsync} from '@/composables/useAsync'
import {useCreateUserForm} from '@/composables/useCreateUserForm'
import {errorMessage, type Schema} from '@/lib/api'
import {groupRoles} from '@/lib/groupRoles'
import {addGroupMember} from '@/lib/groupsApi'
import {fetchUsers} from '@/lib/usersApi'

const props = defineProps<{groupId: string, groupName: string, schoolId: string, memberIds: string[]}>()
const emit = defineEmits<{added: [member: Schema<'GroupMemberDto'>]}>()

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
const newUser = useCreateUserForm()
// Har användaren skapats men inte kunnat läggas till, används samma användare vid nästa försök.
let createdUser: Schema<'UserDto'> | null = null

// Lärare i gruppen är alltid lärare i skolan. Admin och elever kan bara vara elever.
const roleOptions = computed(() => groupRoles.filter((option) =>
  option.value === 'Student' || !creating.value || newUser.form.role === 'Teacher'))

// Den som redan är medlem kan inte läggas till igen. Sökningen matchar namn och e-post.
const items = computed(() => users.value
  .filter((user) => !props.memberIds.includes(user.id))
  .filter((user) => role.value === 'Student' || user.role === 'Teacher')
  .map((user) => ({id: user.id, title: user.name, detail: user.email})))

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  search.value = ''
  userId.value = ''
  role.value = 'Student'
  creating.value = false
  createdUser = null
  newUser.reset()
  const loaded = await loadUsers()
  if (loaded) {
    users.value = loaded
  }
})

// En roll som inte längre finns som val ska inte skickas.
watch(roleOptions, (options) => {
  if (!options.some((option) => option.value === role.value)) {
    role.value = 'Student'
  }
})

// Ett val som rollen döljer ska inte skickas.
watch(items, (list) => {
  if (!list.some((item) => item.id === userId.value)) {
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

async function submit() {
  if (creating.value && !newUser.validate()) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    if (creating.value) {
      createdUser ??= await newUser.create(props.schoolId)
    }
    await addToGroup(creating.value
      ? createdUser
      : users.value.find((found) => found.id === userId.value))
  } catch (exception) {
    error.value = creating.value && createdUser
      ? `Användaren skapades men kunde inte läggas till i gruppen: ${errorMessage(exception)}`
      : errorMessage(exception)
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

        <CreateUserFields v-if="creating" :state="newUser" :roles="['Student', 'Teacher']"/>

        <template v-else>
          <SearchPicker
            id="member-user"
            v-model="userId"
            v-model:search="search"
            label="Användare"
            placeholder="Sök på namn eller e-post"
            :items="items"
            :nouns="['användare', 'användare']"
            :empty-text="role === 'Teacher'
              ? 'Alla lärare är redan med i gruppen.'
              : 'Alla användare är redan med i gruppen.'"
            :loading="loading"
            data-test="member-user"
          />

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
            </Button>.
          </p>
        </template>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="member-role">Roll</FieldLabel>
          <Select v-model="role">
            <SelectTrigger id="member-role" class="w-full" data-test="member-role-select">
              <SelectValue/>
            </SelectTrigger>
            <SelectContent position="popper">
              <SelectItem v-for="option in roleOptions" :key="option.value" :value="option.value">
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
