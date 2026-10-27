<script setup lang="ts">
import {
  PhCalendarBlank,
  PhCaretUpDown,
  PhCheck,
  PhHouse,
  PhListChecks,
  PhMagnifyingGlass,
  PhTray,
  PhUsersThree,
} from '@phosphor-icons/vue'
import type {Component} from 'vue'
import {useRoute} from 'vue-router'

import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from '@/components/ui/sidebar'
import {useCourses} from '@/composables/useCourses'
import {exampleCounts, exampleSchool, exampleUser} from '@/data/exampleData'

interface NavItem {
  label: string
  routeName: string
  icon: Component
  count?: number
}

const mainNav: NavItem[] = [
  {label: 'Idag', routeName: 'today', icon: PhHouse},
  {label: 'Kalender', routeName: 'calendar', icon: PhCalendarBlank},
  {label: 'Inkorg', routeName: 'inbox', icon: PhTray, count: exampleCounts.inbox},
  {label: 'Att bedöma', routeName: 'grading', icon: PhListChecks, count: exampleCounts.grading},
]

const route = useRoute()
const {courses} = useCourses()

function isCourseActive(courseId: string) {
  return route.params.courseId === courseId
}
</script>

<template>
  <Sidebar collapsible="icon">
    <SidebarHeader>
      <SidebarMenu>
        <SidebarMenuItem>
          <DropdownMenu>
            <DropdownMenuTrigger as-child>
              <SidebarMenuButton size="lg" tooltip="Byt skola" data-test="school-switcher">
                <span
                  class="bg-primary text-primary-foreground font-heading flex size-8 shrink-0 items-center justify-center rounded-[7px] text-sm font-bold"
                >R</span>
                <span class="flex flex-1 flex-col leading-tight">
                  <span class="font-semibold">RebornLMS</span>
                  <span class="text-muted-foreground text-xs">{{ exampleSchool.name }}</span>
                </span>
                <PhCaretUpDown class="text-muted-foreground"/>
              </SidebarMenuButton>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" class="w-(--reka-dropdown-menu-trigger-width) min-w-56">
              <DropdownMenuLabel class="text-muted-foreground text-xs">Skola</DropdownMenuLabel>
              <DropdownMenuItem>
                {{ exampleSchool.name }}
                <PhCheck class="ml-auto"/>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </SidebarMenuItem>

        <!-- Sökningen kommer senare, knappen visar var den ska ligga. -->
        <SidebarMenuItem>
          <SidebarMenuButton tooltip="Sök" class="bg-background text-muted-foreground border">
            <PhMagnifyingGlass/>
            <span class="flex-1">Sök…</span>
            <kbd class="bg-muted rounded border px-1 font-mono text-[11px]">⌘K</kbd>
          </SidebarMenuButton>
        </SidebarMenuItem>
      </SidebarMenu>
    </SidebarHeader>

    <SidebarContent>
      <SidebarGroup>
        <SidebarMenu>
          <SidebarMenuItem v-for="item in mainNav" :key="item.routeName">
            <SidebarMenuButton
              as-child
              :tooltip="item.label"
              :is-active="route.name === item.routeName"
            >
              <RouterLink :to="{name: item.routeName}">
                <component :is="item.icon"/>
                <span>{{ item.label }}</span>
              </RouterLink>
            </SidebarMenuButton>
            <SidebarMenuBadge
              v-if="item.count"
              class="bg-primary text-primary-foreground rounded-full"
            >
              {{ item.count }}
            </SidebarMenuBadge>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarGroup>

      <SidebarGroup>
        <SidebarGroupLabel>Mina kurser</SidebarGroupLabel>
        <SidebarMenu>
          <SidebarMenuItem v-for="course in courses" :key="course.id">
            <SidebarMenuButton
              as-child
              :tooltip="course.title"
              :is-active="isCourseActive(course.id)"
            >
              <RouterLink :to="{name: 'course-content', params: {courseId: course.id}}">
                <span class="bg-primary size-2 shrink-0 rounded-[2px]"/>
                <span>{{ course.title }}</span>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarGroup>

      <SidebarGroup>
        <SidebarGroupLabel>Skola</SidebarGroupLabel>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              as-child
              tooltip="Alla grupper"
              :is-active="route.name === 'groups'"
            >
              <RouterLink :to="{name: 'groups'}">
                <PhUsersThree/>
                <span>Alla grupper</span>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarGroup>
    </SidebarContent>

    <SidebarFooter>
      <SidebarMenu>
        <SidebarMenuItem>
          <SidebarMenuButton size="lg" :tooltip="exampleUser.name">
            <Avatar class="size-8">
              <AvatarFallback class="text-xs font-semibold">{{ exampleUser.initials }}</AvatarFallback>
            </Avatar>
            <span class="flex min-w-0 flex-col leading-tight">
              <span class="truncate font-medium">{{ exampleUser.name }}</span>
              <span class="text-muted-foreground truncate text-xs">{{ exampleUser.role }} · {{ exampleSchool.name }}</span>
            </span>
          </SidebarMenuButton>
        </SidebarMenuItem>
      </SidebarMenu>
    </SidebarFooter>

    <SidebarRail/>
  </Sidebar>
</template>
