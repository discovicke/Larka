<script setup lang="ts">
import {ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import NotificationsSection from '@/components/settings/NotificationsSection.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Field, FieldDescription, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, unwrap} from '@/lib/api'

// Första inloggningen med Microsoft: namnet kommer från Microsoft, eller ur skoladressen när Microsoft inte har något, och kan rättas, och notiserna väljs. Allt går att
// ändra senare under Inställningar.
const route = useRoute()
const router = useRouter()
const {me, reloadMe} = useCurrentUser()

const name = ref('')

watch(() => me.value?.name, (value) => {
  if (value && !name.value) {
    name.value = value
  }
}, {immediate: true})

function redirectTarget(): string {
  const redirect = route.query.redirect
  return typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')
    ? redirect
    : '/'
}

const {run: save, loading: saving, error} = useAsync(async () => {
  const trimmed = name.value.trim()
  if (!trimmed) {
    throw new Error('Skriv ditt namn.')
  }
  if (trimmed !== me.value?.name) {
    unwrap(await api.PUT('/api/me/profile', {body: {name: trimmed}}))
    await reloadMe()
  }
  await router.push(redirectTarget())
})
</script>

<template>
  <div class="mx-auto flex w-full max-w-[640px] flex-col gap-6">
    <div class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">Välkommen till Lärka</h1>
      <p class="text-muted-foreground">
        Ditt konto är klart. Kolla att namnet stämmer och välj vilka notiser du vill ha. Du kan ändra båda senare under
        Inställningar.
      </p>
    </div>

    <form class="bg-card flex flex-col gap-4 rounded-xl border p-5" @submit.prevent="save">
      <Field>
        <FieldLabel for="welcome-name">Ditt namn</FieldLabel>
        <Input
          id="welcome-name"
          v-model="name"
          autocomplete="name"
          class="h-10"
          data-test="welcome-name"
        />
        <FieldDescription>Så ser lärare och klasskamrater dig. {{ me?.email }}</FieldDescription>
      </Field>

      <Alert v-if="error" variant="destructive">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>

      <div>
        <Button type="submit" :disabled="saving" data-test="welcome-continue">Fortsätt till Lärka</Button>
      </div>
    </form>

    <NotificationsSection/>
  </div>
</template>
