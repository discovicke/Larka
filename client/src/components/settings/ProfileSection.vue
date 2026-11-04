<script setup lang="ts">
import {PhTrash, PhUploadSimple} from '@phosphor-icons/vue'
import {ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback, AvatarImage} from '@/components/ui/avatar'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Field, FieldLabel} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, multipart, type Schema, unwrap} from '@/lib/api'
import {AVATAR_ACCEPT, avatarProblem} from '@/lib/avatar'
import {initials} from '@/lib/person'

const {me} = useCurrentUser()

const roleLabels: Record<string, string> = {Admin: 'Admin', Teacher: 'Lärare', Student: 'Elev'}

// Namnet går att ändra, som välkomstsidan lovar. E-posten kommer från skolkontot och går inte att ändra.
const name = ref('')
watch(() => me.value?.name, (value) => {
  name.value = value ?? ''
}, {immediate: true})

const {run: saveName, loading: savingName, error: nameError} = useAsync(async () => {
  const trimmed = name.value.trim()
  if (!trimmed) {
    throw new Error('Skriv ditt namn.')
  }
  me.value = unwrap(await api.PUT('/api/me/profile', {body: {name: trimmed}}))
})

const fileInput = ref<HTMLInputElement | null>(null)
// Fel som syns innan något skickas (fel filtyp, för stor fil) visas med samma ruta som fel från servern.
const pickError = ref('')

const {run: upload, loading: uploading, error: uploadError} = useAsync(async (file: File) => {
  const body = new FormData()
  body.append('file', file, file.name)

  // OpenAPI-typen beskriver filen som en sträng, så FormData läggs in här (som för bilagor).
  me.value = unwrap(await api.POST('/api/me/avatar', {
    body: multipart<{file: Schema<'IFormFile'>}>(body),
  }))
})

const {run: remove, loading: removing, error: removeError} = useAsync(async () => {
  me.value = unwrap(await api.DELETE('/api/me/avatar'))
})

async function onFileChanged(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) {
    return
  }

  pickError.value = avatarProblem(file) ?? ''
  if (pickError.value === '') {
    await upload(file)
  }
}

async function onRemove() {
  pickError.value = ''
  await remove()
}
</script>

<template>
  <section id="profil" class="bg-card flex scroll-mt-6 flex-col gap-4 rounded-[11px] border p-5" data-test="profile">
    <header class="flex flex-col gap-0.5">
      <h2 class="text-base font-semibold">Profil</h2>
      <p class="text-muted-foreground text-[13px]">Så ser lärare och klasskamrater dig. E-posten kommer från ditt skolkonto.</p>
    </header>

    <div v-if="me" class="flex flex-wrap items-center gap-4">
      <Avatar class="size-16" data-test="profile-avatar">
        <AvatarImage
          v-if="me.avatarUrl"
          :src="me.avatarUrl"
          alt=""
          data-test="profile-avatar-image"
        />
        <AvatarFallback class="text-[20px] font-semibold">{{ initials(me.name) }}</AvatarFallback>
      </Avatar>
      <div class="flex min-w-0 flex-[1_1_220px] flex-col gap-0.5">
        <span class="text-base font-semibold" data-test="profile-name">{{ me.name }}</span>
        <span class="text-muted-foreground text-[13px] break-words" data-test="profile-line">
          {{ roleLabels[me.role] ?? me.role }} · {{ me.school.name }} · {{ me.email }}
        </span>
      </div>
      <div class="flex flex-wrap gap-2">
        <input
          ref="fileInput"
          type="file"
          :accept="AVATAR_ACCEPT"
          class="sr-only"
          tabindex="-1"
          aria-hidden="true"
          data-test="avatar-input"
          @change="onFileChanged"
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          :disabled="uploading || removing"
          data-test="avatar-upload"
          @click="fileInput?.click()"
        >
          <PhUploadSimple/>
          {{ uploading
            ? 'Laddar upp…'
            : me.avatarUrl
              ? 'Byt bild'
              : 'Ladda upp bild' }}
        </Button>
        <Button
          v-if="me.avatarUrl"
          type="button"
          variant="ghost"
          size="sm"
          :disabled="uploading || removing"
          data-test="avatar-remove"
          @click="onRemove"
        >
          <PhTrash/>
          Ta bort
        </Button>
      </div>
    </div>
    <Skeleton v-else class="h-16 w-full"/>

    <Alert v-if="pickError || uploadError || removeError" variant="destructive" data-test="avatar-error">
      <AlertDescription>{{ pickError || uploadError || removeError }}</AlertDescription>
    </Alert>

    <form
      v-if="me"
      class="flex flex-col gap-2"
      data-test="profile-name-form"
      @submit.prevent="saveName"
    >
      <Field>
        <FieldLabel for="profile-name-input">Namn</FieldLabel>
        <div class="flex flex-wrap gap-2">
          <Input
            id="profile-name-input"
            v-model="name"
            autocomplete="name"
            class="h-9 flex-[1_1_220px]"
            data-test="profile-name-input"
          />
          <Button
            type="submit"
            variant="outline"
            :disabled="savingName || name.trim() === me.name"
            data-test="profile-name-save"
          >
            {{ savingName
              ? 'Sparar…'
              : 'Spara namn' }}
          </Button>
        </div>
      </Field>
      <Alert v-if="nameError" variant="destructive" data-test="profile-name-error">
        <AlertDescription>{{ nameError }}</AlertDescription>
      </Alert>
    </form>

    <div v-if="me" class="flex flex-col gap-2">
      <span class="text-[13px] font-medium">Grupper</span>
      <div v-if="me.groups.length" class="flex flex-wrap gap-1.5" data-test="profile-groups">
        <Badge v-for="group in me.groups" :key="group.id" variant="secondary">{{ group.name }}</Badge>
      </div>
      <p v-else class="text-muted-foreground text-sm" data-test="profile-no-groups">Inga grupper</p>
    </div>
  </section>
</template>
