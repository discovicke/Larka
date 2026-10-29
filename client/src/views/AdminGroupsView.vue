<script setup lang="ts">
import {PhMagnifyingGlass, PhPlus} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'

import DeleteGroupDialog from '@/components/groups/DeleteGroupDialog.vue'
import GroupFormDialog from '@/components/groups/GroupFormDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Empty, EmptyDescription, EmptyHeader, EmptyTitle} from '@/components/ui/empty'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {api, unwrap, type Schema} from '@/lib/api'
import {groupTypeLabels} from '@/lib/groups'
import {fetchMe} from '@/lib/me'

type Group = Schema<'GroupDto'>

const groups = ref<Group[]>([])
const schoolId = ref('')
const loading = ref(true)
const error = ref('')
const search = ref('')

const formOpen = ref(false)
const editing = ref<Group>()
const deleteOpen = ref(false)
const deleting = ref<Group>()

const sorted = computed(() => [...groups.value]
  .sort((left, right) => left.name.localeCompare(right.name, 'sv')))

const visible = computed(() => {
  const text = search.value.trim().toLowerCase()
  return sorted.value.filter((group) => !text
    || group.name.toLowerCase().includes(text)
    || group.term.toLowerCase().includes(text))
})

function rowBorder(index: number) {
  return index < visible.value.length - 1
    ? 'border-b'
    : ''
}

function openCreate() {
  editing.value = undefined
  formOpen.value = true
}

function openEdit(group: Group) {
  editing.value = group
  formOpen.value = true
}

function openDelete(group: Group) {
  deleting.value = group
  deleteOpen.value = true
}

// Listan uppdateras direkt med svaret, utan att hämtas om.
function onSaved(saved: Group) {
  groups.value = groups.value.some((group) => group.id === saved.id)
    ? groups.value.map((group) => group.id === saved.id
        ? saved
        : group)
    : [...groups.value, saved]
}

function onDeleted(removed: Group) {
  groups.value = groups.value.filter((group) => group.id !== removed.id)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    // Nya grupper skapas i användarens egen skola, som i Alla grupper.
    const [me, list] = await Promise.all([fetchMe(), api.GET('/api/groups')])
    schoolId.value = me.school.id
    groups.value = unwrap(list)
  } catch {
    error.value = 'Kunde inte hämta grupperna.'
  } finally {
    loading.value = false
  }
}

load()
</script>

<template>
  <div class="mx-auto flex w-full max-w-[1120px] flex-col gap-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h1 class="font-heading text-[30px] leading-tight font-semibold">Program och klasser</h1>
        <p class="text-muted-foreground">Skolans klasser och grupper. Bara admin kan se sidan.</p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <div class="relative w-full sm:w-[220px]">
          <label for="admin-group-search" class="sr-only">Sök grupp</label>
          <PhMagnifyingGlass class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2"/>
          <Input
            id="admin-group-search"
            v-model="search"
            class="pl-8"
            placeholder="Sök…"
          />
        </div>
        <Button :disabled="!schoolId" data-test="new-group" @click="openCreate">
          <PhPlus/>
          Ny grupp
        </Button>
      </div>
    </div>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <Empty v-else-if="groups.length === 0" class="border border-dashed" data-test="groups-empty">
      <EmptyHeader>
        <EmptyTitle>Inga grupper ännu</EmptyTitle>
        <EmptyDescription>Skapa skolans första klass eller grupp med Ny grupp.</EmptyDescription>
      </EmptyHeader>
    </Empty>

    <section v-else aria-label="Grupper" class="bg-card overflow-hidden rounded-[11px] border">
      <div class="overflow-x-auto">
        <table class="w-full min-w-[680px] border-collapse">
          <thead>
            <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Grupp</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Typ</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Period</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Medlemmar</th>
              <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">
                <span class="sr-only">Åtgärder</span>
              </th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(group, index) in visible" :key="group.id" data-test="group-row">
              <td class="px-4 py-3" :class="rowBorder(index)">
                <RouterLink
                  :to="{name: 'group', params: {groupId: group.id}}"
                  class="font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
                >
                  {{ group.name }}
                </RouterLink>
              </td>
              <td class="px-3 py-3" :class="rowBorder(index)">{{ groupTypeLabels[group.type] }}</td>
              <td class="text-muted-foreground px-3 py-3" :class="rowBorder(index)">{{ group.term || '–' }}</td>
              <td class="px-3 py-3" :class="rowBorder(index)" data-test="member-count">{{ group.memberCount }}</td>
              <td class="px-4 py-3" :class="rowBorder(index)">
                <div class="flex justify-end gap-2">
                  <Button
                    variant="outline"
                    :aria-label="`Redigera ${group.name}`"
                    data-test="edit-group"
                    @click="openEdit(group)"
                  >
                    Redigera
                  </Button>
                  <Button
                    variant="outline"
                    :aria-label="`Ta bort ${group.name}`"
                    data-test="delete-group"
                    @click="openDelete(group)"
                  >
                    Ta bort
                  </Button>
                </div>
              </td>
            </tr>
            <tr v-if="visible.length === 0">
              <td colspan="5" class="text-muted-foreground px-4 py-6 text-center">
                Inga grupper matchar sökningen.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <GroupFormDialog
      v-model:open="formOpen"
      :school-id="schoolId"
      :group="editing"
      @saved="onSaved"
    />
    <DeleteGroupDialog
      v-if="deleting"
      v-model:open="deleteOpen"
      :group="deleting"
      @deleted="onDeleted"
    />
  </div>
</template>
