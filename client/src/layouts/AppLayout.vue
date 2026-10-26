<script setup lang="ts">
import AppHeader from '@/components/layout/AppHeader.vue'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import {SidebarInset, SidebarProvider} from '@/components/ui/sidebar'
import {useCourses} from '@/composables/useCourses'

const {loadCourses} = useCourses()
loadCourses()
</script>

<template>
  <SidebarProvider>
    <AppSidebar/>
    <SidebarInset class="min-w-0">
      <AppHeader/>
      <main class="px-[clamp(16px,3vw,40px)] pt-8 pb-16">
        <div class="mx-auto flex max-w-[1120px] flex-col gap-6">
          <RouterView/>
        </div>
      </main>
    </SidebarInset>
  </SidebarProvider>
</template>
