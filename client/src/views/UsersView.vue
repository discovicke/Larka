<script setup lang="ts">
import {PhDotsThree, PhKey, PhLinkBreak, PhPlus, PhProhibit, PhUserCheck, PhUserGear} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {useRoute} from 'vue-router'

import ChangeRoleDialog from '@/components/users/ChangeRoleDialog.vue'
import CreateUserDialog from '@/components/users/CreateUserDialog.vue'
import DeactivateUserDialog from '@/components/users/DeactivateUserDialog.vue'
import SetPasswordDialog from '@/components/users/SetPasswordDialog.vue'
import UnlinkMicrosoftDialog from '@/components/users/UnlinkMicrosoftDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import PillTabs from '@/components/ui/PillTabs.vue'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import SearchField from '@/components/ui/SearchField.vue'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useToast} from '@/composables/useToast'
import {type Schema} from '@/lib/api'
import {userRoleLabels} from '@/lib/userRoles'
import {fetchAllUsers} from '@/lib/usersApi'

type User = Schema<'UserDto'>
type Filter = 'all' | Schema<'UserRole'> | 'deactivated'

const filters: {value: Filter, label: string}[] = [
  {value: 'all', label: 'Alla'},
  {value: 'Student', label: 'Elever'},
  {value: 'Teacher', label: 'Lärare'},
  {value: 'Admin', label: 'Admin'},
  {value: 'deactivated', label: 'Spärrade'},
]

const {me} = useCurrentUser()

const users = ref<User[]>([])
const route = useRoute()
// Idag länkar hit med ?filter=Student, så att listan redan är avgränsad.
const initialFilter = filters.find((found) => found.value === route.query.filter)
const filter = ref<Filter>(initialFilter?.value ?? 'all')
const search = ref('')

const visibleUsers = computed(() => {
  const text = search.value.trim().toLowerCase()
  return users.value.filter((user) =>
    matchesFilter(user)
    && (!text || user.name.toLowerCase().includes(text) || user.email.toLowerCase().includes(text)))
})

// Spärrade användare syns under sin roll också, med märket Spärrad.
function matchesFilter(user: User) {
  if (filter.value === 'all') {
    return true
  }
  return filter.value === 'deactivated'
    ? !!user.deactivatedAt
    : user.role === filter.value
}

const {showToast} = useToast()
const createOpen = ref(false)

// Den nya användaren står först i listan. Döljer sökningen eller filtret hen, visas alla igen.
function onCreated(created: User) {
  users.value = [created, ...users.value]
  if (!visibleUsers.value.some((user) => user.id === created.id)) {
    filter.value = 'all'
    search.value = ''
  }
  showToast(`${created.name} är skapad.`)
}

const changeRoleOpen = ref(false)
const changingUser = ref<User>()

function openChangeRole(user: User) {
  changingUser.value = user
  changeRoleOpen.value = true
}

const deactivateOpen = ref(false)
const deactivatingUser = ref<User>()

function openDeactivate(user: User) {
  deactivatingUser.value = user
  deactivateOpen.value = true
}

const setPasswordOpen = ref(false)
const passwordUser = ref<User>()

function openSetPassword(user: User) {
  passwordUser.value = user
  setPasswordOpen.value = true
}

const unlinkOpen = ref(false)
const unlinkingUser = ref<User>()

function openUnlink(user: User) {
  unlinkingUser.value = user
  unlinkOpen.value = true
}

function onSaved(saved: User) {
  users.value = users.value.map((user) => user.id === saved.id
    ? saved
    : user)
}

function rowBorder(index: number) {
  return index < visibleUsers.value.length - 1
    ? 'border-b'
    : ''
}

const {run: load, loading, error} = useAsync(async () => {
  users.value = await fetchAllUsers()
}, 'Kunde inte hämta användarna.')

load()
</script>

<template>
  <div class="mx-auto flex w-full max-w-[1120px] flex-col gap-5">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div class="flex flex-col gap-1">
        <h1 class="font-heading text-[30px] leading-tight font-semibold">Användare</h1>
        <p class="text-muted-foreground">Alla i skolan, deras roll och vilka som är spärrade</p>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <PillTabs v-model="filter" :items="filters"/>

        <SearchField
          id="user-search"
          v-model="search"
          class="sm:w-[220px]"
          label="Sök användare"
          placeholder="Sök namn eller e-post…"
        />

        <Button :disabled="!me" data-test="new-user" @click="createOpen = true">
          <PhPlus/>
          Ny användare
        </Button>
      </div>
    </div>

    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <div v-else-if="loading" class="flex flex-col gap-2">
      <Skeleton v-for="i in 4" :key="i" class="h-12 w-full"/>
    </div>

    <section v-else aria-label="Användare" class="bg-card overflow-hidden rounded-[11px] border">
      <div class="overflow-x-auto">
        <table class="w-full border-collapse max-md:block md:min-w-[620px]">
          <thead class="max-md:hidden">
            <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
              <th scope="col" class="border-b px-4 py-2.5 font-medium">Namn</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">E-post</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Roll</th>
              <th scope="col" class="border-b px-3 py-2.5 font-medium">Microsoft</th>
              <th scope="col" class="border-b px-4 py-2.5 text-right font-medium">
                <span class="sr-only">Åtgärder</span>
              </th>
            </tr>
          </thead>
          <tbody class="max-md:block">
            <tr
              v-for="(user, index) in visibleUsers"
              :key="user.id"
              class="max-md:relative max-md:flex max-md:flex-wrap max-md:items-center max-md:gap-x-3 max-md:gap-y-1 max-md:px-4 max-md:py-3"
              :class="index < visibleUsers.length - 1
                ? 'max-md:border-b'
                : ''"
              data-test="user-row"
            >
              <td class="px-4 py-3 font-medium max-md:w-full max-md:border-b-0 max-md:p-0 max-md:pr-10" :class="rowBorder(index)">
                {{ user.name }}
                <span v-if="user.id === me?.id" class="text-muted-foreground font-normal">(du)</span>
                <Badge
                  v-if="user.deactivatedAt"
                  variant="destructive"
                  class="ml-1.5 align-middle"
                  data-test="deactivated-badge"
                >
                  Spärrad
                </Badge>
              </td>
              <td class="text-muted-foreground px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]" :class="rowBorder(index)">
                {{ user.email }}
              </td>
              <td class="px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]" :class="rowBorder(index)" data-test="user-role-cell">
                {{ userRoleLabels[user.role] }}
              </td>
              <td
                class="text-muted-foreground px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]"
                :class="[rowBorder(index), {'max-md:hidden': !user.microsoftLinked}]"
                data-test="user-microsoft-cell"
              >
                {{ user.microsoftLinked
                  ? 'Kopplad'
                  : '–' }}
              </td>
              <td class="w-12 px-3 py-1.5 text-right max-md:absolute max-md:top-2 max-md:right-2 max-md:w-auto max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
                <!-- Ingen kan ändra sin egen roll eller spärra sig själv, så skolan har alltid kvar en admin. -->
                <DropdownMenu v-if="user.id !== me?.id">
                  <DropdownMenuTrigger as-child>
                    <Button
                      variant="ghost"
                      size="icon-sm"
                      :aria-label="`Åtgärder för ${user.name}`"
                      data-test="row-actions"
                    >
                      <PhDotsThree/>
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end" class="min-w-40">
                    <DropdownMenuItem data-test="row-change-role" @select="openChangeRole(user)">
                      <PhUserGear/>
                      Ändra roll
                    </DropdownMenuItem>
                    <!-- Den som loggar in med Microsoft behöver inget lösenord i Lärka. -->
                    <DropdownMenuItem
                      v-if="!user.microsoftLinked"
                      data-test="row-set-password"
                      @select="openSetPassword(user)"
                    >
                      <PhKey/>
                      Sätt lösenord
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      v-if="user.deactivatedAt"
                      data-test="row-reactivate"
                      @select="openDeactivate(user)"
                    >
                      <PhUserCheck/>
                      Återaktivera
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      v-else
                      variant="destructive"
                      data-test="row-deactivate"
                      @select="openDeactivate(user)"
                    >
                      <PhProhibit/>
                      Spärra
                    </DropdownMenuItem>
                    <DropdownMenuItem v-if="user.microsoftLinked" data-test="row-unlink" @select="openUnlink(user)">
                      <PhLinkBreak/>
                      Koppla bort Microsoft
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </td>
            </tr>
            <tr v-if="visibleUsers.length === 0">
              <td colspan="5" class="text-muted-foreground px-4 py-6 text-center">
                Inga användare matchar.
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <CreateUserDialog
      v-if="me"
      v-model:open="createOpen"
      :school-id="me.school.id"
      @created="onCreated"
    />
    <ChangeRoleDialog
      v-if="changingUser"
      v-model:open="changeRoleOpen"
      :user="changingUser"
      @saved="onSaved"
    />
    <DeactivateUserDialog
      v-if="deactivatingUser"
      v-model:open="deactivateOpen"
      :user="deactivatingUser"
      @saved="onSaved"
    />
    <SetPasswordDialog
      v-if="passwordUser"
      v-model:open="setPasswordOpen"
      :user="passwordUser"
      @saved="onSaved"
    />
    <UnlinkMicrosoftDialog
      v-if="unlinkingUser"
      v-model:open="unlinkOpen"
      :user="unlinkingUser"
      @saved="onSaved"
    />
  </div>
</template>
