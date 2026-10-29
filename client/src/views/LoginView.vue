<script setup lang="ts">
import {ref} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import AppLogo from '@/components/layout/AppLogo.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Field, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {exampleSchool} from '@/data/exampleData'
import {auth, AuthError} from '@/lib/auth'

const route = useRoute()
const router = useRouter()
const {resetCurrentUser} = useCurrentUser()

const username = ref('')
const password = ref('')
const sending = ref(false)
const error = ref('')

// Bara en sökväg i appen, inte // som skulle bli en annan värd.
function redirectTarget(): string {
  const redirect = route.query.redirect
  return typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')
    ? redirect
    : '/'
}

async function submit() {
  sending.value = true
  error.value = ''
  try {
    await auth.signIn({username: username.value, password: password.value})
    // En ny användare: glöm den förra, så att layouten hämtar /api/me på nytt i stället för att visa fel roll.
    resetCurrentUser()
    await router.push(redirectTarget())
  } catch (exception) {
    error.value = exception instanceof AuthError && exception.status === 401
      ? 'Fel e-post eller lösenord.'
      : 'Kunde inte logga in. Försök igen.'
  } finally {
    sending.value = false
  }
}
</script>

<template>
  <div class="flex min-h-svh flex-wrap items-stretch">
    <section
      class="bg-sidebar text-sidebar-foreground flex flex-[1_1_420px] flex-col justify-between gap-12 p-[clamp(24px,5vw,64px)]"
    >
      <div class="flex items-center gap-2.5">
        <AppLogo class="size-9"/>
        <span class="text-lg font-semibold">Lärka</span>
      </div>

      <div class="flex max-w-[440px] flex-col gap-4">
        <h1 class="font-heading text-[40px] leading-[1.1] font-semibold">Allt för din utbildning, på ett ställe.</h1>
        <p class="text-sidebar-foreground/80 text-base leading-relaxed">
          Schema, uppgifter, betyg och LIA. Fungerar lika bra på mobilen som på datorn.
        </p>
      </div>

      <div class="text-sidebar-foreground/60 text-[13px]">{{ exampleSchool.name }}</div>
    </section>

    <main class="flex flex-[1_1_480px] items-center justify-center px-4 py-12">
      <div class="flex w-full max-w-[380px] flex-col gap-6">
        <div class="flex flex-col gap-1">
          <h2 class="font-heading text-[26px] font-semibold">Logga in</h2>
          <p class="text-muted-foreground text-sm">{{ exampleSchool.name }}</p>
        </div>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <form class="flex flex-col gap-3.5" @submit.prevent="submit">
          <Field>
            <FieldLabel for="login-username">Användarnamn eller e-post</FieldLabel>
            <Input
              id="login-username"
              v-model="username"
              type="text"
              autocomplete="username"
              class="h-10"
              required
            />
          </Field>

          <Field>
            <FieldLabel for="login-password">Lösenord</FieldLabel>
            <Input
              id="login-password"
              v-model="password"
              type="password"
              autocomplete="current-password"
              class="h-10"
              required
            />
          </Field>

          <Button type="submit" class="h-11 w-full" :disabled="sending">
            {{
              sending
                ? 'Loggar in…'
                : 'Logga in'
            }}
          </Button>
        </form>
      </div>
    </main>
  </div>
</template>
