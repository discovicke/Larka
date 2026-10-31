<script setup lang="ts">
import {Badge} from '@/components/ui/badge'
import {Skeleton} from '@/components/ui/skeleton'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {initials} from '@/lib/person'

const {me} = useCurrentUser()

const roleLabels: Record<string, string> = {Admin: 'Admin', Teacher: 'Lärare', Student: 'Elev'}
</script>

<template>
  <section id="profil" class="bg-card flex scroll-mt-6 flex-col gap-4 rounded-[11px] border p-5" data-test="profile">
    <header class="flex flex-col gap-0.5">
      <h2 class="text-base font-semibold">Profil</h2>
      <p class="text-muted-foreground text-[13px]">Namn och e-post kommer från skolans register</p>
    </header>

    <div v-if="me" class="flex items-center gap-4">
      <div
        class="bg-muted flex size-16 shrink-0 items-center justify-center rounded-full text-[20px] font-semibold"
        aria-hidden="true"
      >
        {{ initials(me.name) }}
      </div>
      <div class="flex min-w-0 flex-col gap-0.5">
        <span class="text-base font-semibold" data-test="profile-name">{{ me.name }}</span>
        <span class="text-muted-foreground text-[13px] break-words" data-test="profile-line">
          {{ roleLabels[me.role] ?? me.role }} · {{ me.school.name }} · {{ me.email }}
        </span>
      </div>
    </div>
    <Skeleton v-else class="h-16 w-full"/>

    <div v-if="me" class="flex flex-col gap-2">
      <span class="text-[13px] font-medium">Grupper</span>
      <div v-if="me.groups.length" class="flex flex-wrap gap-1.5" data-test="profile-groups">
        <Badge v-for="group in me.groups" :key="group.id" variant="secondary">{{ group.name }}</Badge>
      </div>
      <p v-else class="text-muted-foreground text-sm" data-test="profile-no-groups">Inga grupper</p>
    </div>
  </section>
</template>
