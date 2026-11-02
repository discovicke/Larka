<script setup lang="ts">
import {PhWindowsLogo} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema, unwrap} from '@/lib/api'

// Microsoft-inloggning för skolan. Appen ställs in med infra/entra-setup.sh, och här kopplar admin skolan till
// appens tenant så att inloggningar därifrån hamnar i skolan.
const {me} = useCurrentUser()
const microsoft = ref<Schema<'SchoolMicrosoftDto'> | null>(null)

const schoolId = computed(() => me.value?.school?.id)

const badge = computed<{label: string, variant: 'success' | 'outline'}>(() => {
  if (!microsoft.value?.configured) {
    return {label: 'Ej inställd', variant: 'outline'}
  }
  return microsoft.value.linked
    ? {label: 'På', variant: 'success'}
    : {label: 'Av', variant: 'outline'}
})

const {run: load, loading, error: loadError} = useAsync(async (id: string) => {
  microsoft.value = unwrap(await api.GET('/api/schools/{id}/microsoft', {params: {path: {id}}}))
}, 'Kunde inte hämta Microsoft-inloggningen.')

const {run: setLinked, loading: saving, error: saveError} = useAsync(async (link: boolean) => {
  const id = schoolId.value!
  microsoft.value = unwrap(link
    ? await api.PUT('/api/schools/{id}/microsoft', {params: {path: {id}}})
    : await api.DELETE('/api/schools/{id}/microsoft', {params: {path: {id}}}))
}, 'Kunde inte spara. Försök igen.')

watch(schoolId, (id) => {
  if (id) {
    load(id)
  }
}, {immediate: true})
</script>

<template>
  <div class="bg-card rounded-xl border p-5" data-test="microsoft-integration">
    <header class="flex flex-wrap items-start justify-between gap-3">
      <div class="flex items-center gap-2.5">
        <PhWindowsLogo class="size-5 shrink-0"/>
        <div class="flex flex-col gap-0.5">
          <h3 class="text-base font-semibold">Inloggning med Microsoft</h3>
          <p class="text-muted-foreground text-sm">
            Elever och personal loggar in med skolans Microsoft-konto.
          </p>
        </div>
      </div>
      <Badge :variant="badge.variant" data-test="microsoft-status">{{ badge.label }}</Badge>
    </header>

    <Skeleton v-if="loading && !microsoft" class="mt-4 h-16 w-full"/>
    <Alert v-else-if="loadError" variant="destructive" class="mt-4">
      <AlertDescription>{{ loadError }}</AlertDescription>
    </Alert>
    <template v-else-if="microsoft">
      <p v-if="!microsoft.configured" class="text-muted-foreground mt-4 text-sm" data-test="microsoft-not-configured">
        Skolans Entra-admin ställer in inloggningen en gång med <code>infra/entra-setup.sh</code>. Sedan kan du slå på
        den här.
      </p>
      <template v-else>
        <dl class="mt-4 grid gap-3 text-sm">
          <div class="flex flex-col gap-0.5">
            <dt class="text-muted-foreground text-xs">Tenant</dt>
            <dd class="font-mono break-all" data-test="microsoft-tenant">{{ microsoft.tenantId }}</dd>
          </div>
        </dl>
        <p class="text-muted-foreground mt-3 text-sm">
          {{ microsoft.linked
            ? 'Första gången någon loggar in blir hen elev, i klassen som e-postadressen slutar på om den finns. Du gör lärare och placerar övriga under Användare.'
            : 'När du slår på inloggningen kan alla med ett konto i skolans Microsoft logga in.' }}
        </p>
        <Alert v-if="saveError" variant="destructive" class="mt-3">
          <AlertDescription>{{ saveError }}</AlertDescription>
        </Alert>
        <div class="mt-4">
          <Button
            :variant="microsoft.linked
              ? 'outline'
              : 'default'"
            :disabled="saving"
            data-test="microsoft-toggle"
            @click="setLinked(!microsoft.linked)"
          >
            {{ microsoft.linked
              ? 'Stäng av inloggning med Microsoft'
              : 'Slå på inloggning med Microsoft' }}
          </Button>
        </div>
      </template>
    </template>
  </div>
</template>
