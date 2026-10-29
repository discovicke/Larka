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
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {api, errorMessage, type Schema} from '@/lib/api'
import {groupRoles} from '@/lib/groupRoles'

const props = defineProps<{groupId: string, groupName: string, memberIds: string[]}>()
const emit = defineEmits<{added: [member: Schema<'GroupMemberDto'>]}>()

const open = defineModel<boolean>('open', {required: true})

const users = ref<Schema<'UserDto'>[]>([])
const search = ref('')
const userId = ref('')
const role = ref<Schema<'GroupRole'>>('Student')
const loading = ref(false)
const saving = ref(false)
const error = ref('')

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
  error.value = ''
  loading.value = true
  try {
    const {data} = await api.GET('/api/users')
    users.value = data ?? []
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
})

// Ett val som filtret döljer ska inte skickas.
watch(candidates, (list) => {
  if (!list.some((user) => user.id === userId.value)) {
    userId.value = ''
  }
})

async function add() {
  const user = users.value.find((found) => found.id === userId.value)
  if (!user) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    await api.POST('/api/groups/{id}/members', {
      params: {path: {id: props.groupId}},
      body: {userId: user.id, role: role.value},
    })
    emit('added', {userId: user.id, name: user.name, role: role.value})
    open.value = false
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="add">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Lägg till medlem i {{ groupName }}</DialogTitle>
          <DialogDescription>Välj en användare och vilken roll hen har i gruppen.</DialogDescription>
        </DialogHeader>

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
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button type="submit" :disabled="saving || userId === ''" data-test="confirm-add-member">
            Lägg till
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
