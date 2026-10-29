<script setup lang="ts">
import {computed} from 'vue'
import {useRoute} from 'vue-router'

import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbList,
  BreadcrumbPage,
  BreadcrumbSeparator,
} from '@/components/ui/breadcrumb'
import {Separator} from '@/components/ui/separator'
import {SidebarTrigger} from '@/components/ui/sidebar'
import type {Breadcrumb as Crumb} from '@/router'

const route = useRoute()

// Sidor utan egna brödsmulor visar bara sitt namn.
const breadcrumbs = computed<Crumb[]>(() => {
  if (route.meta.breadcrumbs) {
    return route.meta.breadcrumbs(route)
  }
  return route.meta.title
    ? [{label: route.meta.title}]
    : []
})
</script>

<template>
  <header class="bg-background flex min-h-14 shrink-0 flex-wrap items-center gap-2 border-b px-4 py-2 md:px-6">
    <SidebarTrigger aria-label="Visa/dölj sidomeny" class="size-7"/>
    <Separator orientation="vertical" class="data-[orientation=vertical]:h-4 data-[orientation=vertical]:self-center"/>

    <Breadcrumb class="min-w-0 flex-1 basis-0 pl-1">
      <BreadcrumbList class="flex-nowrap">
        <template v-for="(crumb, index) in breadcrumbs" :key="index">
          <!-- Under sm visas bara de två sista, så att långa spår inte spricker headern. -->
          <BreadcrumbSeparator v-if="index > 0" :class="index < breadcrumbs.length - 1 && 'max-sm:hidden'"/>
          <BreadcrumbItem
            :class="[
              index === 0 && breadcrumbs.length > 1
                ? 'shrink-0'
                : index === breadcrumbs.length - 1 && index > 0
                  ? 'max-w-[60%] shrink-0'
                  : 'min-w-0',
              index < breadcrumbs.length - 2 && 'max-sm:hidden',
            ]"
          >
            <BreadcrumbPage v-if="index === breadcrumbs.length - 1" class="truncate font-medium">
              {{ crumb.label }}
            </BreadcrumbPage>
            <BreadcrumbLink v-else-if="crumb.to" as-child>
              <RouterLink :to="crumb.to" class="truncate">{{ crumb.label }}</RouterLink>
            </BreadcrumbLink>
            <span v-else>{{ crumb.label }}</span>
          </BreadcrumbItem>
        </template>
      </BreadcrumbList>
    </Breadcrumb>

    <!-- Sidornas knappar (t.ex. Avbryt / Spara utkast / Publicera) hamnar här via <PageActions>. -->
    <div id="page-actions" class="ml-auto flex shrink-0 items-center gap-2"/>
  </header>
</template>
