<script setup lang="ts">
import {PhGithubLogo} from '@phosphor-icons/vue'
import {computed} from 'vue'

import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {fetchGitHubAuthorizeUrl, unlinkGitHub} from '@/lib/integrations'

const {me, reloadMe} = useCurrentUser()

const {
  run: connect,
  loading: connecting,
  error: connectError,
} = useAsync(async () => {
  window.location.href = await fetchGitHubAuthorizeUrl('/settings')
}, 'Kunde inte öppna GitHub.')

const {
  run: unlink,
  loading: unlinking,
  error: unlinkError,
} = useAsync(async () => {
  await unlinkGitHub()
  await reloadMe()
}, 'Kunde inte koppla från GitHub.')

const loading = computed(() => connecting.value || unlinking.value)
const error = computed(() => connectError.value || unlinkError.value)
</script>

<template>
  <section class="bg-card rounded-xl border p-5" data-test="user-github">
    <header class="flex flex-wrap items-start justify-between gap-3">
      <div class="flex items-center gap-2.5">
        <PhGithubLogo class="size-5 shrink-0"/>
        <div class="flex flex-col gap-0.5">
          <h2 class="text-base font-semibold">Ditt GitHub-konto</h2>
          <p class="text-muted-foreground text-sm">
            Anslut ditt GitHub-konto för att få åtkomst till dina kursrepon och lämna in koduppgifter.
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
          ? 'Ansluten'
          : 'Inte ansluten' }}
      </Badge>
    </header>

    <div class="mt-4 flex flex-wrap items-center gap-3">
      <template v-if="me?.gitHubLogin">
        <span class="text-sm">
          Ansluten som <strong class="font-mono" data-test="user-github-login">@{{ me.gitHubLogin }}</strong>
        </span>
        <Button
          variant="outline"
          size="sm"
          :disabled="loading"
          data-test="user-github-disconnect"
          @click="unlink"
        >
          Ta bort anslutning
        </Button>
      </template>
      <template v-else>
        <Button
          size="sm"
          type="button"
          :disabled="loading"
          data-test="user-github-connect"
          @click="connect"
        >
          <PhGithubLogo class="size-4"/>
          {{ connecting
            ? 'Laddar…'
            : 'Anslut GitHub' }}
        </Button>
      </template>
    </div>
    <p v-if="error" class="text-destructive mt-2 text-sm" data-test="user-github-error">
      {{ error }}
    </p>
  </section>
</template>
