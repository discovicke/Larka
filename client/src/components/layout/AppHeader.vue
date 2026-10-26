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

    <Breadcrumb class="min-w-0 pl-1">
      <BreadcrumbList class="flex-nowrap">
        <template v-for="(crumb, index) in breadcrumbs" :key="crumb.label">
          <BreadcrumbSeparator v-if="index > 0"/>
          <BreadcrumbItem
            :class="index === breadcrumbs.length - 1
              ? 'min-w-0'
              : 'shrink-0'"
          >
            <BreadcrumbPage v-if="index === breadcrumbs.length - 1" class="truncate font-medium">
              {{ crumb.label }}
            </BreadcrumbPage>
            <BreadcrumbLink v-else-if="crumb.to" as-child>
              <RouterLink :to="crumb.to">{{ crumb.label }}</RouterLink>
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
