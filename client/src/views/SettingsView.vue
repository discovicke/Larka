<script setup lang="ts">
import {computed, onMounted, ref} from 'vue'

import AppearanceSection from '@/components/settings/AppearanceSection.vue'
import IntegrationsSection from '@/components/settings/IntegrationsSection.vue'
import NotificationsSection from '@/components/settings/NotificationsSection.vue'
import ProfileSection from '@/components/settings/ProfileSection.vue'
import UserGitHubSection from '@/components/settings/UserGitHubSection.vue'
import {useCurrentUser} from '@/composables/useCurrentUser'

const {me, loadMe, can} = useCurrentUser()
const githubInUse = computed(() => me.value?.gitHubInUse === true)
const isAdmin = computed(() => can('admin'))

const navItems = computed(() => [
  {id: 'profil', label: 'Profil'},
  ...(githubInUse.value
    ? [{id: 'konton', label: 'Anslutna konton'}]
    : []),
  {id: 'aviseringar', label: 'Aviseringar'},
  {id: 'utseende', label: 'Utseende'},
  ...(isAdmin.value
    ? [{id: 'integrationer', label: 'Integrationer'}]
    : []),
])
const activeSection = ref('profil')

onMounted(loadMe)
</script>

<template>
  <div class="mx-auto flex w-full max-w-[1040px] flex-col gap-6">
    <header class="flex flex-col gap-1">
      <h1 class="font-heading text-[30px] leading-tight font-semibold">Inställningar</h1>
      <p class="text-muted-foreground">Din profil, aviseringar och utseende.</p>
    </header>

    <div class="flex w-full flex-wrap items-start gap-8">
      <nav aria-label="Inställningar" class="flex flex-[1_1_180px] flex-col gap-0.5">
        <a
          v-for="item in navItems"
          :key="item.id"
          :href="`#${item.id}`"
          :aria-current="activeSection === item.id
            ? 'location'
            : undefined"
          :data-test="`nav-${item.id}`"
          :class="[
            'rounded-md px-2.5 py-1.5 text-sm transition-colors hover:bg-muted/60',
            activeSection === item.id
              ? 'bg-muted text-foreground font-medium'
              : 'text-muted-foreground',
          ]"
          @click="activeSection = item.id"
        >
          {{ item.label }}
        </a>
      </nav>

      <div class="flex min-w-0 flex-[999_1_560px] flex-col gap-6">
        <ProfileSection/>

        <section v-if="githubInUse" id="konton" class="flex scroll-mt-6 flex-col gap-6">
          <UserGitHubSection/>
        </section>

        <NotificationsSection/>

        <AppearanceSection/>

        <IntegrationsSection v-if="isAdmin"/>
      </div>
    </div>
  </div>
</template>
