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
import {FieldLabel} from '@/components/ui/field'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useAsync} from '@/composables/useAsync'
import {errorMessage, type Schema} from '@/lib/api'
import {addGroupMember, changeMemberRole, removeGroupMember} from '@/lib/groupsApi'
import {fetchUsers} from '@/lib/usersApi'

const props = defineProps<{groupId: string, groupName: string, members: Schema<'GroupMemberDto'>[]}>()
// changed kommer när mentorn är satt, också om den förra mentorn inte kunde tas bort, så att sidan visar läget.
const emit = defineEmits<{changed: []}>()

const open = defineModel<boolean>('open', {required: true})

const users = ref<Schema<'UserDto'>[]>([])
const userId = ref('')
const saving = ref(false)
// Samma felruta visar felet från hämtningen och från sparandet.
const {run: loadUsers, loading, error} = useAsync(fetchUsers)

const mentors = computed(() => props.members.filter((member) => member.role === 'Mentor'))
// Mentorn är en lärare eller admin i skolan. En som redan är mentor väljs inte igen.
const candidates = computed(() => users.value
  .filter((user) => user.role !== 'Student')
  .filter((user) => !mentors.value.some((mentor) => mentor.userId === user.id))
  .sort((first, second) => first.name.localeCompare(second.name, 'sv')))
const chosen = computed(() => candidates.value.find((user) => user.id === userId.value))

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  userId.value = ''
  const loaded = await loadUsers()
  if (loaded) {
    users.value = loaded
  }
})

async function save() {
  if (!chosen.value) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    // Mentor är en roll i klassen. Är läraren redan med byter rollen, annars läggs hen till.
    const isMember = props.members.some((member) => member.userId === chosen.value!.id)
    await (isMember
      ? changeMemberRole(props.groupId, chosen.value.id, 'Mentor')
      : addGroupMember(props.groupId, chosen.value.id, 'Mentor'))
  } catch (exception) {
    error.value = `Kunde inte utse ${chosen.value.name} till mentor. ${errorMessage(exception)}`
    saving.value = false
    return
  }

  try {
    // En klass har en mentor, så den förra lämnar klassen.
    for (const mentor of mentors.value) {
      await removeGroupMember(props.groupId, mentor.userId)
    }
    emit('changed')
    open.value = false
  } catch (exception) {
    emit('changed')
    error.value = `${chosen.value.name} är mentor, men den förra mentorn kunde inte tas bort ur klassen. ${errorMessage(exception)}`
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[calc(100dvh-2rem)] grid-cols-[minmax(0,1fr)] overflow-y-auto sm:max-w-md">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="save">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">
            {{ mentors.length > 0
              ? 'Byt mentor'
              : 'Utse mentor' }} för {{ groupName }}
          </DialogTitle>
          <DialogDescription>
            Mentorn läser klasslistan och följer klassen. Välj en lärare eller admin.
          </DialogDescription>
        </DialogHeader>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="mentor-user">Ny mentor</FieldLabel>
          <NativeSelect
            id="mentor-user"
            v-model="userId"
            class="w-full"
            :disabled="loading || saving"
            data-test="mentor-user"
          >
            <NativeSelectOption value="">
              {{ candidates.length === 0 && !loading
                ? 'Inga lärare hittades'
                : 'Välj lärare…' }}
            </NativeSelectOption>
            <NativeSelectOption v-for="user in candidates" :key="user.id" :value="user.id">
              {{ user.name }} · {{ user.email }}
            </NativeSelectOption>
          </NativeSelect>
        </div>

        <p v-if="mentors.length > 0 && chosen" class="text-muted-foreground text-sm" data-test="mentor-leaves">
          {{ mentors.map((mentor) => mentor.name).join(', ') }} lämnar klassen när du byter.
        </p>

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
          <Button type="submit" :disabled="saving || !chosen" data-test="confirm-mentor">
            Utse till mentor
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
