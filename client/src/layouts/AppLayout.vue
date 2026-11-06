<script setup lang="ts">
import {useEventListener} from '@vueuse/core'
import {watch} from 'vue'
import {useRoute} from 'vue-router'

import AppHeader from '@/components/layout/AppHeader.vue'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import AppToaster from '@/components/layout/AppToaster.vue'
import {SidebarInset, SidebarProvider} from '@/components/ui/sidebar'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useNotifications} from '@/composables/useNotifications'

const route = useRoute()

const {loadCourses} = useCourses()
const {loadMe} = useCurrentUser()
const {loadUnreadCount} = useNotifications()
loadCourses()
loadMe()
loadUnreadCount()

// Märket i sidomenyn hämtas igen när man byter sida eller kommer tillbaka till fliken, så att en ny notis syns
// utan att man laddar om.
watch(() => route.path, loadUnreadCount)
useEventListener(window, 'focus', loadUnreadCount)
</script>

<template>
  <SidebarProvider>
    <AppSidebar/>
    <SidebarInset class="min-w-0">
      <AppHeader/>
      <main
        v-if="route.meta.fullBleed"
        data-test="main-full-bleed"
        class="flex min-h-[calc(100svh-56px)] min-w-0"
      >
        <RouterView/>
      </main>
      <main v-else data-test="main-padded" class="px-[clamp(16px,3vw,40px)] pt-5 pb-6 md:pt-8 md:pb-16">
        <div class="mx-auto flex max-w-[1120px] flex-col gap-5 md:gap-6">
          <RouterView/>
        </div>
      </main>
    </SidebarInset>
    <AppToaster/>
  </SidebarProvider>
</template>
