<script setup lang="ts">
import {ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Field, FieldDescription, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useToast} from '@/composables/useToast'
import {api, unwrap} from '@/lib/api'

// Skolans namn, som inloggningssidan och sidomenyn visar. Bara admin ser sektionen. En skola utan Microsoft
// heter annars som migrationens skola.
const {me} = useCurrentUser()

const name = ref('')
watch(() => me.value?.school.name, (value) => {
  name.value = value ?? ''
}, {immediate: true})

const {run: save, loading: saving, error} = useAsync(async () => {
  const school = me.value?.school
  const trimmed = name.value.trim()
  if (!school) {
    return
  }
  if (!trimmed) {
    throw new Error('Skriv skolans namn.')
  }
  const saved = unwrap(await api.PUT('/api/schools/{id}', {params: {path: {id: school.id}}, body: {name: trimmed}}))
  if (me.value) {
    me.value = {...me.value, school: {id: saved.id, name: saved.name}}
  }
  useToast().showToast('Skolans namn är sparat.')
})
</script>

<template>
  <section id="skola" class="bg-card flex scroll-mt-6 flex-col gap-4 rounded-[11px] border p-5" data-test="school">
    <header class="flex flex-col gap-0.5">
      <h2 class="text-base font-semibold">Skola</h2>
      <p class="text-muted-foreground text-[13px]">Gäller hela skolan. Bara admin kan ändra det.</p>
    </header>

    <form
      v-if="me"
      class="flex flex-col gap-2"
      data-test="school-name-form"
      @submit.prevent="save"
    >
      <Field>
        <FieldLabel for="school-name-input">Skolans namn</FieldLabel>
        <div class="flex flex-wrap gap-2">
          <Input
            id="school-name-input"
            v-model="name"
            autocomplete="organization"
            class="h-9 flex-[1_1_220px]"
            data-test="school-name-input"
          />
          <Button
            type="submit"
            variant="outline"
            :disabled="saving || name.trim() === me.school.name"
            data-test="school-name-save"
          >
            {{ saving
              ? 'Sparar…'
              : 'Spara namn' }}
          </Button>
        </div>
        <FieldDescription>Visas på inloggningssidan och i sidomenyn.</FieldDescription>
      </Field>
      <Alert v-if="error" variant="destructive" data-test="school-name-error">
        <AlertDescription>{{ error }}</AlertDescription>
      </Alert>
    </form>
  </section>
</template>
