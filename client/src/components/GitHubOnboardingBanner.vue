<script setup lang="ts">
import {PhGithubLogo, PhX} from '@phosphor-icons/vue'
import {computed, ref} from 'vue'
import {useRoute} from 'vue-router'

import {Button} from '@/components/ui/button'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {errorMessage} from '@/lib/api'
import {fetchGitHubAuthorizeUrl} from '@/lib/integrations'

const dismissedKey = 'github_onboarding_dismissed'
const route = useRoute()
const {me} = useCurrentUser()
const dismissed = ref(sessionStorage.getItem(dismissedKey) === '1')
const connecting = ref(false)
const error = ref('')

const visible = computed(() => !dismissed.value
  && me.value?.role === 'Student'
  && me.value.gitHubInUse === true
  && !me.value.gitHubLogin)

function dismiss() {
  dismissed.value = true
  sessionStorage.setItem(dismissedKey, '1')
}

async function connect() {
  connecting.value = true
  error.value = ''
  try {
    window.location.href = await fetchGitHubAuthorizeUrl(route.fullPath)
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    connecting.value = false
  }
}
</script>

<template>
  <div
    v-if="visible"
    class="border-primary/20 bg-primary/5 flex flex-wrap items-center gap-3 rounded-xl border p-3.5"
    data-test="github-onboarding"
  >
    <span class="bg-primary/10 text-primary grid size-9 shrink-0 place-items-center rounded-lg">
      <PhGithubLogo class="size-5"/>
    </span>
    <div class="flex min-w-0 flex-1 flex-col gap-0.5">
      <p class="text-sm font-medium">Anslut ditt GitHub-konto</p>
      <p class="text-muted-foreground text-xs">
        Då får du åtkomst till dina kursrepon och kan lämna in koduppgifter via GitHub.
      </p>
      <p v-if="error" class="text-destructive text-xs">{{ error }}</p>
    </div>
    <Button
      type="button"
      size="sm"
      :disabled="connecting"
      data-test="github-onboarding-connect"
      @click="connect"
    >
      <PhGithubLogo class="size-4"/>
      {{ connecting
        ? 'Öppnar GitHub…'
        : 'Anslut GitHub' }}
    </Button>
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      aria-label="Stäng"
      data-test="github-onboarding-dismiss"
      @click="dismiss"
    >
      <PhX/>
    </Button>
  </div>
</template>
