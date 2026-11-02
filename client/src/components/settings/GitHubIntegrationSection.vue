<script setup lang="ts">
import {
  PhArrowClockwise,
  PhArrowSquareOut,
  PhGithubLogo,
  PhWarning,
} from '@phosphor-icons/vue'
import {computed, onMounted, ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import {Alert, AlertDescription, AlertTitle} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {formatDate} from '@/lib/content'
import {
  connectGitHub,
  disconnectGitHub,
  fetchGitHubConnection,
  fetchGitHubInstallUrl,
  setGitHubEnabled,
  type GitHubConnection,
} from '@/lib/integrations'

const route = useRoute()
const router = useRouter()
const {reloadMe} = useCurrentUser()
const installStateKey = 'github_install_state'
const queryError = ref('')
const github = ref<GitHubConnection | null>(null)

const {
  run: loadStatus,
  loading: loadingStatus,
  error: loadError,
} = useAsync(fetchGitHubConnection, 'Kunde inte hämta GitHub-statusen.')

const {
  run: runAction,
  loading: acting,
  error: actionError,
} = useAsync(async (action: () => Promise<GitHubConnection | void>) => {
  const result = await action()
  if (result) {
    github.value = result
  }
  await reloadMe()
}, 'Kunde inte uppdatera GitHub.')

const githubLoading = computed(() => loadingStatus.value || acting.value)
const githubError = computed(() => queryError.value || loadError.value || actionError.value)

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

const facts = computed(() => [
  {label: 'Organisation', value: github.value?.org || '–', test: 'github-org', mono: true},
  {label: 'GitHub-konto', value: github.value?.accountLogin || '–', test: 'github-account', mono: true},
  {label: 'App ID', value: github.value?.appId || '–', test: 'github-app-id', mono: true},
  {label: 'Installations-ID', value: github.value?.installationId || '–', test: 'github-installation-id', mono: true},
  {
    label: 'Elever med GitHub',
    value: `${github.value?.linkedStudentCount ?? 0} av ${github.value?.studentCount ?? 0}`,
    test: 'github-students',
  },
  {label: 'Repon', value: github.value?.repoCount ?? 0, test: 'github-repos'},
  {label: 'API-adress', value: github.value?.apiBaseUrl || '–', test: 'github-api-url', mono: true, wide: true},
])

async function refresh() {
  const status = await loadStatus()
  if (status) {
    github.value = status
  }
}

async function clearSetupQuery() {
  if (route.query.installation_id
    || route.query.setup_action
    || route.query.state
    || route.query.github
    || route.query.github_error) {
    await router.replace({path: '/settings'})
  }
}

function connect() {
  if (!github.value) {
    return
  }

  void runAction(async () => {
    // En sparad men lokalt frånkopplad installation kan verifieras igen utan en ny installation.
    if (github.value!.installationId > 0 && !github.value!.usingLegacyInstallation) {
      return connectGitHub()
    }

    const link = await fetchGitHubInstallUrl()
    sessionStorage.setItem(installStateKey, link.state)
    window.location.href = link.url
  })
}

function disconnect() {
  void runAction(async () => {
    await disconnectGitHub()
    await refresh()
  })
}

function toggleEnabled(enabled: boolean) {
  void runAction(() => setGitHubEnabled(enabled))
}

async function handleSetup(installationId: number) {
  const queryState = typeof route.query.state === 'string'
    ? route.query.state
    : null
  const state = queryState ?? sessionStorage.getItem(installStateKey)
  if (!state) {
    queryError.value = 'GitHub-anslutningen kunde inte verifieras. Försök igen från Inställningar.'
    await clearSetupQuery()
    return
  }

  await runAction(async () => {
    const status = await connectGitHub(installationId, state)
    sessionStorage.removeItem(installStateKey)
    return status
  })
  await clearSetupQuery()
}

onMounted(async () => {
  const setupError = route.query.github_error
  if (typeof setupError === 'string') {
    await refresh()
    queryError.value = setupError
    await clearSetupQuery()
    return
  }

  const installationId = Number(route.query.installation_id)
  if (Number.isInteger(installationId) && installationId > 0) {
    await handleSetup(installationId)
    return
  }

  await refresh()
  if (route.query.github === 'connected') {
    await clearSetupQuery()
  }
})
</script>

<template>
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
        <div
          v-for="fact in facts"
          :key="fact.test"
          class="flex flex-col gap-0.5"
          :class="fact.wide && 'sm:col-span-2'"
        >
          <dt class="text-muted-foreground text-xs">{{ fact.label }}</dt>
          <dd
            :class="fact.mono && 'font-mono'"
            :data-test="fact.test"
          >
            {{ fact.value }}
          </dd>
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
          @update:model-value="(value) => toggleEnabled(value === true)"
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
          @click="connect"
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
          @click="disconnect"
        >
          Koppla från
        </Button>
        <Button
          variant="outline"
          size="sm"
          :disabled="githubLoading"
          @click="refresh"
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
      <Alert
        v-if="github.connected"
        variant="warning"
        class="mt-4"
        data-test="github-security-warning"
      >
        <PhWarning/>
        <AlertTitle>Kontrollera organisationens basbehörighet</AlertTitle>
        <AlertDescription>
          <p class="text-xs">
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
        </AlertDescription>
      </Alert>
    </template>

    <Button
      v-if="githubError && !github"
      variant="outline"
      size="sm"
      class="mt-3"
      :disabled="githubLoading"
      @click="refresh"
    >
      <PhArrowClockwise/>
      Försök igen
    </Button>
  </div>
</template>
