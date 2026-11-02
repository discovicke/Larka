<script setup lang="ts">
import {
  PhArrowSquareOut,
  PhCheckCircle,
  PhClock,
  PhGitCommit,
  PhGithubLogo,
  PhLock,
  PhXCircle,
} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'

import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {errorMessage, type Schema} from '@/lib/api'
import {fetchGitHubAuthorizeUrl} from '@/lib/integrations'

const props = withDefaults(
  defineProps<{
    hasRepoMode?: boolean
    gitHubLogin?: string | null
    repoUrl?: string | null
    groupName?: string | null
    groupMembers?: string[]
    latestCommit?: string | null
    checkStatus?: 'Pending' | 'Green' | 'Red' | null
    lockedAt?: string | null
    submissionSha?: string | null
    lockError?: string | null
    repoProvisioningState?: 'Pending' | 'Creating' | 'Ready' | 'Failed' | null
    repoProvisioningError?: string | null
    requirements?: Schema<'AssignmentRequirementDto'>[]
    checkRuns?: Schema<'GitHubCheckRunDto'>[]
    authorizeUrl?: string | null
    redirectTo?: string | null
  }>(),
  {
    hasRepoMode: true,
    gitHubLogin: null,
    repoUrl: null,
    groupName: null,
    groupMembers: () => [],
    latestCommit: null,
    checkStatus: null,
    lockedAt: null,
    submissionSha: null,
    lockError: null,
    repoProvisioningState: null,
    repoProvisioningError: null,
    requirements: () => [],
    checkRuns: () => [],
    authorizeUrl: null,
    redirectTo: null,
  },
)

const emit = defineEmits<{
  connect: []
}>()

const connecting = ref(false)
const connectError = ref('')

const statusBadge = computed<{
  label: string
  variant: 'default' | 'outline' | 'destructive'
  icon: typeof PhClock | typeof PhCheckCircle | typeof PhXCircle
}>(() => {
  if (props.checkStatus === 'Green') {
    return {label: 'Tester godkända', variant: 'default', icon: PhCheckCircle}
  }
  if (props.checkStatus === 'Red') {
    return {label: 'Tester underkända', variant: 'destructive', icon: PhXCircle}
  }
  return {label: 'Tester körs / väntar', variant: 'outline', icon: PhClock}
})

const requirementRows = computed(() => props.requirements.map((requirement) => {
  if (requirement.isManual) {
    return {requirement, label: 'Bedöms av lärare', variant: 'outline' as const}
  }

  const pattern = requirement.testPattern ?? ''
  const matches = props.checkRuns.filter((run) => wildcardMatch(pattern, run.name))
  if (matches.length === 0) {
    const label = props.checkRuns.length > 0
      ? 'Ingen matchande check'
      : 'Väntar på test'
    return {requirement, label, variant: 'outline' as const}
  }

  const failed = matches.some((run) =>
    run.status !== 'completed'
    || (run.conclusion !== 'success'
      && run.conclusion !== 'neutral'
      && run.conclusion !== 'skipped'))
  return failed
    ? {requirement, label: 'Underkänd', variant: 'destructive' as const}
    : {requirement, label: 'Godkänd', variant: 'default' as const}
}))

function wildcardMatch(pattern: string, value: string): boolean {
  if (!pattern.trim()) {
    return false
  }

  const expression = pattern
    .split('*')
    .map((part) => part.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
    .join('.*')
  if (new RegExp(`^${expression}$`, 'i').test(value)) {
    return true
  }

  // Matrix-jobb får ett suffix, t.ex. "build (ubuntu-latest)". Ett krav på jobbnamnet ska
  // fortfarande matcha när läraren inte själv har skrivit ett wildcard.
  return !pattern.includes('*')
    && (value.toLowerCase().startsWith(`${pattern.toLowerCase()} (`)
      || value.toLowerCase().startsWith(`${pattern.toLowerCase()} / `))
}

async function handleConnect() {
  emit('connect')
  if (props.authorizeUrl) {
    window.location.href = props.authorizeUrl
    return
  }

  connecting.value = true
  connectError.value = ''
  try {
    const url = await fetchGitHubAuthorizeUrl(props.redirectTo ?? undefined)
    window.location.href = url
  } catch (exception) {
    connectError.value = errorMessage(exception)
  } finally {
    connecting.value = false
  }
}
</script>

<template>
  <section
    v-if="hasRepoMode"
    class="bg-card flex flex-col gap-4 rounded-xl border p-5"
    data-test="github-assignment-block"
  >
    <header class="flex flex-wrap items-center justify-between gap-3">
      <div class="flex items-center gap-2.5">
        <PhGithubLogo class="size-5 shrink-0"/>
        <div>
          <h3 class="text-base font-semibold">GitHub-inlämning</h3>
          <p class="text-muted-foreground text-xs">
            Automatisk testning via GitHub Actions
          </p>
        </div>
      </div>
      <Badge
        v-if="repoUrl && lockedAt"
        variant="outline"
        class="gap-1.5"
        data-test="student-repo-locked-badge"
      >
        <PhLock class="size-3.5"/>
        Repot är låst
      </Badge>
      <Badge
        v-else-if="repoUrl && checkStatus"
        :variant="statusBadge.variant"
        class="gap-1.5"
        data-test="student-repo-status"
      >
        <component :is="statusBadge.icon" class="size-3.5"/>
        {{ statusBadge.label }}
      </Badge>
      <Badge
        v-else-if="repoProvisioningState === 'Failed'"
        variant="destructive"
        data-test="student-repo-provisioning-failed"
      >
        Repot kunde inte skapas
      </Badge>
      <Badge
        v-else-if="gitHubLogin"
        variant="outline"
        class="gap-1.5"
        data-test="student-repo-provisioning"
      >
        <PhClock class="size-3.5"/>
        Repot skapas
      </Badge>
    </header>

    <!-- Fall 1: Eleven har inte kopplat GitHub -->
    <div
      v-if="!gitHubLogin"
      class="flex flex-col items-start gap-3 rounded-lg border border-dashed p-4"
      data-test="github-unlinked"
    >
      <p class="text-sm">
        Du har inte kopplat något GitHub-konto till din profil. Koppla ditt konto för att få ett personligt uppgiftsrepo.
      </p>
      <Button
        size="sm"
        data-test="github-connect-cta"
        @click="handleConnect"
      >
        <PhGithubLogo class="size-4"/>
        Koppla ditt GitHub-konto
      </Button>
    </div>

    <!-- Fall 2a: Repot kunde inte skapas -->
    <div
      v-else-if="!repoUrl && repoProvisioningState === 'Failed'"
      class="flex flex-col gap-2 rounded-lg border border-destructive/30 bg-destructive/5 p-4 text-sm"
      data-test="github-provisioning-failed"
    >
      <p class="font-medium">Ditt uppgiftsrepo kunde inte skapas.</p>
      <p class="text-muted-foreground text-xs">
        {{ repoProvisioningError || 'Kontakta din lärare så att GitHub-inställningarna kan kontrolleras.' }}
      </p>
    </div>

    <!-- Fall 2b: Eleven är kopplad men repot skapas fortfarande -->
    <div
      v-else-if="!repoUrl"
      class="flex flex-col gap-1 rounded-lg border border-dashed p-4 text-sm"
      data-test="github-pending-repo"
    >
      <p class="font-medium">
        Ansluten som <span class="font-mono text-xs">@{{ gitHubLogin }}</span>
      </p>
      <p class="text-muted-foreground text-xs">
        Ditt personliga uppgiftsrepo skapas i skolans organisation. Detta kan ta några sekunder.
      </p>
    </div>

    <!-- Fall 3: Repot är klart -->
    <div
      v-else
      class="flex flex-col gap-3 text-sm"
      data-test="github-repo-ready"
    >
      <div v-if="groupName" class="flex flex-col gap-1" data-test="student-group">
        <span class="text-muted-foreground text-xs">Din grupp</span>
        <span class="text-sm font-medium">{{ groupName }}</span>
        <span v-if="groupMembers.length > 0" class="text-muted-foreground text-xs">
          {{ groupMembers.join(', ') }}
        </span>
      </div>

      <div class="flex flex-col gap-1">
        <span class="text-muted-foreground text-xs">Ditt repo</span>
        <a
          :href="repoUrl"
          target="_blank"
          rel="noopener noreferrer"
          class="text-link hover:underline inline-flex items-center gap-1.5 font-mono text-sm font-medium break-all"
          data-test="student-repo-link"
        >
          <span>{{ repoUrl }}</span>
          <PhArrowSquareOut class="size-3.5 shrink-0"/>
        </a>
      </div>

      <div v-if="latestCommit" class="flex items-center gap-1.5 text-xs text-muted-foreground" data-test="student-repo-commit">
        <PhGitCommit class="size-3.5 shrink-0"/>
        <span>Senaste commit: <code class="font-mono">{{ latestCommit.slice(0, 7) }}</code></span>
      </div>

      <div
        v-if="lockedAt"
        class="flex flex-col gap-1 rounded-lg border border-dashed p-3 text-xs"
        data-test="student-repo-locked"
      >
        <p v-if="submissionSha" class="text-sm font-medium">
          Inlämnad commit:
          <code class="font-mono text-xs">{{ submissionSha.slice(0, 7) }}</code>
        </p>
        <p v-else-if="lockError" class="text-muted-foreground">
          {{ lockError }}
        </p>
        <p class="text-muted-foreground">
          Repot låstes vid deadline och går inte att ändra. Kontakta läraren om du behöver komplettera.
        </p>
      </div>

      <ul v-if="requirementRows.length > 0" class="flex flex-col gap-1.5" data-test="student-requirements">
        <li
          v-for="row in requirementRows"
          :key="row.requirement.id"
          class="flex items-center justify-between gap-3 rounded-md border px-2.5 py-1.5"
        >
          <span>{{ row.requirement.title }}</span>
          <Badge :variant="row.variant">{{ row.label }}</Badge>
        </li>
      </ul>
    </div>
  </section>
</template>
