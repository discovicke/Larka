<script setup lang="ts">
import {
  PhArrowSquareOut,
  PhCheckCircle,
  PhClock,
  PhGitCommit,
  PhGithubLogo,
  PhXCircle,
} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'

import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {errorMessage} from '@/lib/api'
import {fetchGitHubAuthorizeUrl} from '@/lib/integrations'

const props = withDefaults(
  defineProps<{
    hasRepoMode?: boolean
    gitHubLogin?: string | null
    repoUrl?: string | null
    latestCommit?: string | null
    checkStatus?: 'Pending' | 'Green' | 'Red' | null
    repoProvisioningState?: 'Pending' | 'Creating' | 'Ready' | 'Failed' | null
    repoProvisioningError?: string | null
    authorizeUrl?: string | null
    redirectTo?: string | null
  }>(),
  {
    hasRepoMode: true,
    gitHubLogin: null,
    repoUrl: null,
    latestCommit: null,
    checkStatus: null,
    repoProvisioningState: null,
    repoProvisioningError: null,
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
        v-if="repoUrl && checkStatus"
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
        Kopplad som <span class="font-mono text-xs">@{{ gitHubLogin }}</span>
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
    </div>
  </section>
</template>
