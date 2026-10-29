<script setup lang="ts">
import {
  PhArrowClockwise,
  PhArrowSquareOut,
  PhCaretRight,
  PhCircleHalf,
  PhGithubLogo,
  PhMoon,
  PhSun,
} from '@phosphor-icons/vue'
import {computed, onMounted, ref, type Component} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {Field, FieldContent, FieldDescription, FieldLabel, FieldLegend, FieldSet} from '@/components/ui/field'
import {RadioGroup, RadioGroupItem} from '@/components/ui/radio-group'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useTheme} from '@/composables/useTheme'
import {errorMessage} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {
  connectGitHub,
  disconnectGitHub,
  fetchGitHubAuthorizeUrl,
  fetchGitHubConnection,
  fetchGitHubInstallUrl,
  setGitHubEnabled,
  unlinkGitHub,
  type GitHubConnection,
} from '@/lib/integrations'
import {initials} from '@/lib/news'
import {fetchNotificationSettings, saveNotificationSettings} from '@/lib/notificationSettings'
import type {Schema} from '@/lib/api'
import type {ThemePreference} from '@/lib/theme'

const route = useRoute()
const router = useRouter()
const {preference, resolvedTheme} = useTheme()
const {me, loadMe, reloadMe, can} = useCurrentUser()

const githubInstallStateKey = 'github_install_state'

const isAdmin = computed(() => can('admin'))
const github = ref<GitHubConnection | null>(null)
const githubLoading = ref(false)
const githubError = ref('')
const userGitHubLoading = ref(false)
const userGitHubError = ref('')

const githubBadge = computed<{
  label: string
  variant: 'default' | 'outline' | 'destructive'
}>(() => {
  if (!github.value) {
    return githubError.value
      ? {label: 'Fel', variant: 'destructive'}
      : {label: 'Laddar', variant: 'outline'}
  }
  if (!github.value.configured) {
    return {label: 'Ej konfigurerad', variant: 'outline'}
  }
  if (!github.value.connected) {
    return {label: 'Inte ansluten', variant: 'destructive'}
  }
  return github.value.enabled
    ? {label: 'Ansluten', variant: 'default'}
    : {label: 'Pausad', variant: 'outline'}
})

const missingSettings = computed(() => {
  const labels: Record<string, string> = {
    AppId: 'App ID',
    AppSlug: 'App-slug',
    PrivateKey: 'Privat nyckel',
  }
  return (github.value?.missingSettings ?? []).map((setting) => labels[setting] ?? setting)
})

const lastVerified = computed(() => github.value?.lastVerifiedAt
  ? formatDate(github.value.lastVerifiedAt, true)
  : '–')

const manageGitHubUrl = computed(() => github.value?.accountLogin
  ? `https://github.com/organizations/${encodeURIComponent(github.value.accountLogin)}/settings/installations/${github.value.installationId}`
  : null)

const memberPrivilegesUrl = computed(() => github.value?.accountLogin
  ? `https://github.com/organizations/${encodeURIComponent(github.value.accountLogin)}/settings/member_privileges`
  : null)

async function loadGitHubStatus() {
  if (!isAdmin.value) {
    return
  }

  githubLoading.value = true
  githubError.value = ''
  try {
    github.value = await fetchGitHubConnection()
  } catch (exception) {
    githubError.value = errorMessage(exception)
  } finally {
    githubLoading.value = false
  }
}

async function handleConnectGitHub() {
  if (!github.value) {
    return
  }

  githubLoading.value = true
  githubError.value = ''
  try {
    // En sparad men lokalt frånkopplad installation kan verifieras igen utan en ny installation.
    // Legacy-installationen från miljövariablerna räknas inte: den ska kopplas till skolan i databasen.
    if (github.value.installationId > 0 && !github.value.usingLegacyInstallation) {
      github.value = await connectGitHub()
      return
    }

    const link = await fetchGitHubInstallUrl()
    sessionStorage.setItem(githubInstallStateKey, link.state)
    window.location.href = link.url
  } catch (exception) {
    githubError.value = errorMessage(exception)
  } finally {
    githubLoading.value = false
  }
}

async function handleGitHubSetup(installationId: number) {
  const queryState = typeof route.query.state === 'string'
    ? route.query.state
    : null
  const state = queryState ?? sessionStorage.getItem(githubInstallStateKey)
  if (!state) {
    githubError.value = 'GitHub-anslutningen kunde inte verifieras. Försök igen från Inställningar.'
    await clearGitHubSetupQuery()
    return
  }

  githubLoading.value = true
  githubError.value = ''
  try {
    github.value = await connectGitHub(installationId, state)
    sessionStorage.removeItem(githubInstallStateKey)
  } catch (exception) {
    githubError.value = errorMessage(exception)
  } finally {
    githubLoading.value = false
    await clearGitHubSetupQuery()
  }
}

async function clearGitHubSetupQuery() {
  if (route.query.installation_id
    || route.query.setup_action
    || route.query.state
    || route.query.github
    || route.query.github_error) {
    await router.replace({path: '/settings'})
  }
}

async function handleDisconnectGitHub() {
  githubLoading.value = true
  githubError.value = ''
  try {
    await disconnectGitHub()
    await loadGitHubStatus()
  } catch (exception) {
    githubError.value = errorMessage(exception)
  } finally {
    githubLoading.value = false
  }
}

async function handleToggleGitHubEnabled(enabled: boolean) {
  githubLoading.value = true
  githubError.value = ''
  try {
    github.value = await setGitHubEnabled(enabled)
  } catch (exception) {
    githubError.value = errorMessage(exception)
  } finally {
    githubLoading.value = false
  }
}

async function handleConnectUserGitHub() {
  userGitHubLoading.value = true
  userGitHubError.value = ''
  try {
    const url = await fetchGitHubAuthorizeUrl('/settings')
    window.location.href = url
  } catch (exception) {
    userGitHubError.value = errorMessage(exception)
  } finally {
    userGitHubLoading.value = false
  }
}

async function handleUnlinkUserGitHub() {
  userGitHubLoading.value = true
  userGitHubError.value = ''
  try {
    await unlinkGitHub()
    await reloadMe()
  } catch (exception) {
    userGitHubError.value = errorMessage(exception)
  } finally {
    userGitHubLoading.value = false
  }
}

const roleLabels: Record<string, string> = {Admin: 'Admin', Teacher: 'Lärare', Student: 'Elev'}

type NotificationKind = Schema<'NotificationKind'>

// En rad i tabellen kan styra flera slag: betyg och begäran om komplettering hör ihop för eleven.
const notificationRows: {id: string, label: string, kinds: NotificationKind[]}[] = [
  {id: 'feedback', label: 'Betyg och återkoppling', kinds: ['SubmissionGraded', 'SubmissionNeedsWork']},
  {id: 'deadline', label: 'Påminnelse 24 h före deadline', kinds: ['DeadlineSoon']},
  {id: 'assignments', label: 'Nya uppgifter', kinds: ['AssignmentPublished']},
  {id: 'news', label: 'Nyheter', kinds: ['NewsPublished']},
]

const muted = ref<NotificationKind[]>([])
const notificationsLoaded = ref(false)
const notificationError = ref('')

const navItems = computed(() => [
  {id: 'profil', label: 'Profil'},
  {id: 'konton', label: 'Kopplade konton'},
  {id: 'aviseringar', label: 'Aviseringar'},
  {id: 'utseende', label: 'Utseende'},
  ...(isAdmin.value
    ? [{id: 'integrationer', label: 'Integrationer'}]
    : []),
])
const activeSection = ref('profil')

function isEnabled(kinds: NotificationKind[]) {
  return !kinds.some((kind) => muted.value.includes(kind))
}

async function toggleNotification(kinds: NotificationKind[], enabled: boolean) {
  const previous = muted.value
  muted.value = enabled
    ? previous.filter((kind) => !kinds.includes(kind))
    : [...new Set([...previous, ...kinds])]
  notificationError.value = ''
  try {
    muted.value = (await saveNotificationSettings({muted: muted.value})).muted
  } catch (exception) {
    muted.value = previous
    notificationError.value = errorMessage(exception)
  }
}

async function loadNotificationSettings() {
  try {
    muted.value = (await fetchNotificationSettings()).muted
    notificationsLoaded.value = true
  } catch (exception) {
    notificationError.value = errorMessage(exception)
  }
}

onMounted(async () => {
  void loadNotificationSettings()
  await loadMe()
  if (!isAdmin.value) {
    return
  }

  const setupError = route.query.github_error
  if (typeof setupError === 'string') {
    await loadGitHubStatus()
    githubError.value = setupError
    await clearGitHubSetupQuery()
    return
  }

  const installationId = Number(route.query.installation_id)
  if (Number.isInteger(installationId) && installationId > 0) {
    await handleGitHubSetup(installationId)
    return
  }

  await loadGitHubStatus()
  if (route.query.github === 'connected') {
    await clearGitHubSetupQuery()
  }
})

const options: {
  value: ThemePreference
  label: string
  description: string
  icon: Component
}[] = [
  {
    value: 'auto',
    label: 'Automatiskt',
    description: 'Följer systemets inställning för ljust eller mörkt.',
    icon: PhCircleHalf,
  },
  {
    value: 'light',
    label: 'Ljust',
    description: 'Använd ljust tema oavsett systeminställning.',
    icon: PhSun,
  },
  {
    value: 'dark',
    label: 'Mörkt',
    description: 'Använd mörkt tema oavsett systeminställning.',
    icon: PhMoon,
  },
]
</script>

<template>
  <div class="mx-auto flex w-full max-w-[1040px] flex-col gap-6">
    <header class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">Inställningar</h1>
      <p class="text-muted-foreground">Din profil, aviseringar och utseende.</p>
    </header>

    <div class="flex w-full flex-wrap items-start gap-8">
      <nav aria-label="Inställningar" class="flex flex-[1_1_180px] flex-col gap-0.5">
        <a
          v-for="item in navItems"
          :key="item.id"
          :href="`#${item.id}`"
          :aria-current="activeSection === item.id
            ? 'location'
            : undefined"
          :data-test="`nav-${item.id}`"
          :class="[
            'rounded-md px-2.5 py-1.5 text-sm transition-colors hover:bg-muted/60',
            activeSection === item.id
              ? 'bg-muted text-foreground font-medium'
              : 'text-muted-foreground',
          ]"
          @click="activeSection = item.id"
        >
          {{ item.label }}
        </a>
      </nav>

      <div class="flex min-w-0 flex-[999_1_560px] flex-col gap-6">
        <section id="profil" class="bg-card flex scroll-mt-6 flex-col gap-4 rounded-[11px] border p-5" data-test="profile">
          <header class="flex flex-col gap-0.5">
            <h2 class="text-base font-semibold">Profil</h2>
            <p class="text-muted-foreground text-[13px]">Namn och e-post kommer från skolans register</p>
          </header>

          <div v-if="me" class="flex items-center gap-4">
            <div
              class="bg-muted flex size-16 shrink-0 items-center justify-center rounded-full text-[20px] font-semibold"
              aria-hidden="true"
            >
              {{ initials(me.name) }}
            </div>
            <div class="flex min-w-0 flex-col gap-0.5">
              <span class="text-base font-semibold" data-test="profile-name">{{ me.name }}</span>
              <span class="text-muted-foreground text-[13px] break-words" data-test="profile-line">
                {{ roleLabels[me.role] ?? me.role }} · {{ me.school.name }} · {{ me.email }}
              </span>
            </div>
          </div>
          <Skeleton v-else class="h-16 w-full"/>

          <div v-if="me" class="flex flex-col gap-2">
            <span class="text-[13px] font-medium">Grupper</span>
            <div v-if="me.groups.length" class="flex flex-wrap gap-1.5" data-test="profile-groups">
              <Badge v-for="group in me.groups" :key="group.id" variant="secondary">{{ group.name }}</Badge>
            </div>
            <p v-else class="text-muted-foreground text-sm" data-test="profile-no-groups">Inga grupper</p>
          </div>
        </section>

        <section id="konton" class="flex scroll-mt-6 flex-col gap-6">
          <!-- Elevens/användarens eget GitHub-konto -->
          <section class="bg-card rounded-xl border p-5" data-test="user-github">
            <header class="flex flex-wrap items-start justify-between gap-3">
              <div class="flex items-center gap-2.5">
                <PhGithubLogo class="size-5 shrink-0"/>
                <div class="flex flex-col gap-0.5">
                  <h2 class="text-base font-semibold">Ditt GitHub-konto</h2>
                  <p class="text-muted-foreground text-sm">
                    Koppla ditt konto för att få tillgång till personliga uppgiftsrepon.
                  </p>
                </div>
              </div>
              <Badge
                :variant="me?.gitHubLogin
                  ? 'default'
                  : 'outline'"
                data-test="user-github-status"
              >
                {{ me?.gitHubLogin
                  ? 'Kopplad'
                  : 'Inte kopplad' }}
              </Badge>
            </header>

            <div class="mt-4 flex flex-wrap items-center gap-3">
              <template v-if="me?.gitHubLogin">
                <span class="text-sm">
                  Kopplad som <strong class="font-mono" data-test="user-github-login">@{{ me.gitHubLogin }}</strong>
                </span>
                <Button
                  variant="outline"
                  size="sm"
                  :disabled="userGitHubLoading"
                  data-test="user-github-disconnect"
                  @click="handleUnlinkUserGitHub"
                >
                  Koppla från
                </Button>
              </template>
              <template v-else>
                <Button
                  size="sm"
                  type="button"
                  :disabled="userGitHubLoading"
                  data-test="user-github-connect"
                  @click="handleConnectUserGitHub"
                >
                  <PhGithubLogo class="size-4"/>
                  {{ userGitHubLoading
                    ? 'Laddar…'
                    : 'Koppla GitHub' }}
                </Button>
              </template>
            </div>
            <p v-if="userGitHubError" class="text-destructive mt-2 text-sm" data-test="user-github-error">
              {{ userGitHubError }}
            </p>
          </section>
        </section>

        <section
          id="aviseringar"
          class="bg-card scroll-mt-6 overflow-hidden rounded-[11px] border"
          data-test="notifications"
        >
          <header class="flex flex-col gap-0.5 px-5 pt-5 pb-3">
            <h2 class="text-base font-semibold">Aviseringar</h2>
            <p class="text-muted-foreground text-[13px]">Välj vad du vill få i appen</p>
          </header>
          <Alert
            v-if="notificationError"
            variant="destructive"
            class="mx-5 mb-3 w-auto"
            data-test="notifications-error"
          >
            <AlertDescription>{{ notificationError }}</AlertDescription>
          </Alert>
          <table class="w-full text-sm">
            <thead>
              <tr class="text-muted-foreground text-[13px]">
                <th scope="col" class="px-5 py-2 text-left font-normal">Händelse</th>
                <th scope="col" class="w-[90px] px-5 py-2 text-center font-normal">I appen</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in notificationRows" :key="row.id" class="border-t">
                <td class="px-5 py-2.5">{{ row.label }}</td>
                <td class="px-5 py-2.5">
                  <div class="flex justify-center">
                    <Checkbox
                      :model-value="isEnabled(row.kinds)"
                      :disabled="!notificationsLoaded"
                      :aria-label="`${row.label} i appen`"
                      :data-test="`notify-${row.id}`"
                      @update:model-value="(checked) => toggleNotification(row.kinds, checked === true)"
                    />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </section>

        <section id="utseende" class="bg-card scroll-mt-6 rounded-xl border p-5">
          <FieldSet>
            <FieldLegend>Utseende</FieldLegend>
            <FieldDescription>
              Välj om appen ska följa systemet eller alltid använda ljust eller mörkt tema.
            </FieldDescription>

            <RadioGroup v-model="preference" aria-label="Tema" class="gap-2">
              <FieldLabel
                v-for="option in options"
                :key="option.value"
                :for="`theme-${option.value}`"
                class="has-data-checked:border-primary has-data-checked:ring-primary hover:bg-muted/50 w-full cursor-pointer items-center gap-3 rounded-lg border p-3 font-normal transition-colors has-data-checked:ring-1"
              >
                <Field orientation="horizontal">
                  <FieldContent class="gap-0.5">
                    <span class="flex items-center gap-2">
                      <component :is="option.icon" class="text-muted-foreground size-4 shrink-0"/>
                      <span class="font-medium">{{ option.label }}</span>
                    </span>
                    <FieldDescription>{{ option.description }}</FieldDescription>
                  </FieldContent>
                  <RadioGroupItem
                    :id="`theme-${option.value}`"
                    :value="option.value"
                    :data-test="`theme-${option.value}`"
                  />
                </Field>
              </FieldLabel>
            </RadioGroup>
          </FieldSet>

          <p class="text-muted-foreground mt-4 text-sm">
            Just nu används
            <strong class="text-foreground" data-test="resolved-theme">
              {{ resolvedTheme === 'dark'
                ? 'mörkt tema'
                : 'ljust tema' }}
            </strong>.
          </p>
        </section>

        <section
          v-if="isAdmin"
          id="integrationer"
          class="scroll-mt-6 flex flex-col gap-3"
          data-test="integrations"
        >
          <header class="flex flex-col gap-1">
            <h2 class="font-heading text-xl font-semibold">Integrationer</h2>
            <p class="text-muted-foreground text-sm">
              Anslutningar som gäller hela skolan. Bara admin kan se dem.
            </p>
          </header>

          <div class="bg-card rounded-xl border p-5" data-test="github-integration">
            <header class="flex flex-wrap items-start justify-between gap-3">
              <div class="flex items-center gap-2.5">
                <PhGithubLogo class="size-5 shrink-0"/>
                <div class="flex flex-col gap-0.5">
                  <h3 class="text-base font-semibold">GitHub</h3>
                  <p class="text-muted-foreground text-sm">
                    Elevrepon skapas i skolans GitHub-organisation.
                  </p>
                </div>
              </div>
              <Badge :variant="githubBadge.variant" data-test="github-status">
                {{ githubBadge.label }}
              </Badge>
            </header>

            <Skeleton v-if="githubLoading && !github" class="mt-4 h-28 w-full"/>
            <Alert v-else-if="githubError && !github" variant="destructive" class="mt-4">
              <AlertDescription>{{ githubError }}</AlertDescription>
            </Alert>
            <template v-else-if="github">
              <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-2">
                <div class="flex flex-col gap-0.5">
                  <dt class="text-muted-foreground text-xs">Organisation</dt>
                  <dd class="font-mono" data-test="github-org">{{ github.org || '–' }}</dd>
                </div>
                <div class="flex flex-col gap-0.5">
                  <dt class="text-muted-foreground text-xs">GitHub-konto</dt>
                  <dd class="font-mono" data-test="github-account">{{ github.accountLogin || '–' }}</dd>
                </div>
                <div class="flex flex-col gap-0.5">
                  <dt class="text-muted-foreground text-xs">App ID</dt>
                  <dd class="font-mono" data-test="github-app-id">{{ github.appId || '–' }}</dd>
                </div>
                <div class="flex flex-col gap-0.5">
                  <dt class="text-muted-foreground text-xs">Installations-ID</dt>
                  <dd class="font-mono" data-test="github-installation-id">
                    {{ github.installationId || '–' }}
                  </dd>
                </div>
                <div class="flex flex-col gap-0.5">
                  <dt class="text-muted-foreground text-xs">Elever med GitHub</dt>
                  <dd data-test="github-students">
                    {{ github.linkedStudentCount }} av {{ github.studentCount }}
                  </dd>
                </div>
                <div class="flex flex-col gap-0.5">
                  <dt class="text-muted-foreground text-xs">Repon</dt>
                  <dd data-test="github-repos">{{ github.repoCount }}</dd>
                </div>
                <div class="flex flex-col gap-0.5 sm:col-span-2">
                  <dt class="text-muted-foreground text-xs">API-adress</dt>
                  <dd class="font-mono break-all" data-test="github-api-url">{{ github.apiBaseUrl }}</dd>
                </div>
              </dl>

              <label
                v-if="github.connected"
                class="mt-4 flex items-center gap-2.5 text-sm"
                data-test="github-enabled"
              >
                <Checkbox
                  :model-value="github.enabled"
                  :disabled="githubLoading"
                  @update:model-value="(value) => handleToggleGitHubEnabled(value === true)"
                />
                <span>
                  Aktivera GitHub för skolan
                  <span class="text-muted-foreground block text-xs">
                    Pausa utan att koppla från appen eller påverka befintliga repon.
                  </span>
                </span>
              </label>

              <p
                v-if="missingSettings.length"
                class="text-destructive mt-4 text-sm"
                data-test="github-missing"
              >
                Saknas: {{ missingSettings.join(', ') }}
              </p>
              <p v-if="github.error" class="text-destructive mt-2 text-sm" data-test="github-error">
                {{ github.error }}
              </p>
              <p
                v-if="github.usingLegacyInstallation"
                class="text-muted-foreground mt-2 text-sm"
                data-test="github-legacy"
              >
                Skolan använder serverns gamla GitHub-inställning. Koppla skolan för att spara installationen
                i databasen i stället.
              </p>

              <div class="mt-4 flex flex-wrap items-center gap-3">
                <Button
                  v-if="github.configured && (!github.connected || github.usingLegacyInstallation)"
                  size="sm"
                  :disabled="githubLoading || missingSettings.length > 0"
                  data-test="github-connect"
                  @click="handleConnectGitHub"
                >
                  {{ github.usingLegacyInstallation
                    ? 'Koppla skolan till GitHub'
                    : github.installationId > 0
                      ? 'Återanslut'
                      : 'Anslut GitHub' }}
                </Button>
                <Button
                  v-if="manageGitHubUrl"
                  variant="outline"
                  size="sm"
                  as-child
                >
                  <a
                    :href="manageGitHubUrl"
                    target="_blank"
                    rel="noopener noreferrer"
                    data-test="github-manage"
                  >
                    <PhArrowSquareOut/>
                    Hantera på GitHub
                  </a>
                </Button>
                <Button
                  v-if="github.connected"
                  variant="destructive"
                  size="sm"
                  :disabled="githubLoading"
                  data-test="github-disconnect"
                  @click="handleDisconnectGitHub"
                >
                  Koppla från
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  :disabled="githubLoading"
                  @click="loadGitHubStatus"
                >
                  <PhArrowClockwise/>
                  {{ githubLoading
                    ? 'Uppdaterar…'
                    : 'Uppdatera status' }}
                </Button>
                <span class="text-muted-foreground text-xs" data-test="github-verified">
                  Senast verifierad {{ lastVerified }}
                </span>
              </div>
              <p class="text-muted-foreground mt-3 text-xs">
                Frånkoppling tar bara bort kopplingen här. GitHub-appen och elevrepon påverkas inte.
              </p>
              <div
                v-if="github.connected"
                class="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-200"
                data-test="github-security-warning"
              >
                <p class="font-medium">Kontrollera organisationens basbehörighet</p>
                <p class="mt-1 text-xs">
                  Om medlemmar har <strong>Read</strong> eller <strong>Write</strong> som basbehörighet ser
                  alla elever alla privata repon, inklusive mallar och klasskamraters repon. Sätt
                  <strong>Member privileges → Base permissions</strong> till <strong>No permission</strong>.
                  Stäng också av medlemmars repo-skapande, radering och ändring av visibility.
                </p>
                <a
                  v-if="memberPrivilegesUrl"
                  :href="memberPrivilegesUrl"
                  target="_blank"
                  rel="noopener noreferrer"
                  class="mt-2 inline-flex items-center gap-1 text-xs font-medium underline underline-offset-3"
                  data-test="github-member-privileges"
                >
                  Öppna medlemsinställningar
                  <PhArrowSquareOut class="size-3.5"/>
                </a>
              </div>
            </template>

            <Button
              v-if="githubError && !github"
              variant="outline"
              size="sm"
              class="mt-3"
              :disabled="githubLoading"
              @click="loadGitHubStatus"
            >
              <PhArrowClockwise/>
              Försök igen
            </Button>
          </div>
        </section>

        <section v-if="isAdmin" class="flex flex-col gap-3" data-test="admin-groups-entry">
          <RouterLink
            :to="{name: 'admin-groups'}"
            class="bg-card hover:bg-muted/40 flex items-center justify-between gap-3 rounded-xl border p-5"
          >
            <div class="flex flex-col gap-0.5">
              <h2 class="text-base font-semibold">Program och klasser</h2>
              <p class="text-muted-foreground text-sm">Skapa, ändra och ta bort skolans klasser och grupper.</p>
            </div>
            <PhCaretRight class="text-muted-foreground size-4 shrink-0"/>
          </RouterLink>
        </section>
      </div>
    </div>
  </div>
</template>
