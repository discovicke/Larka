<script setup lang="ts">
import {
  PhCalendarBlank,
  PhCaretUpDown,
  PhCheck,
  PhFolder,
  PhGearSix,
  PhGraduationCap,
  PhHouse,
  PhListChecks,
  PhMagnifyingGlass,
  PhSignOut,
  PhTray,
  PhUsersThree,
} from '@phosphor-icons/vue'
import {type Component, computed, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

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
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useGradingCount} from '@/composables/useGradingCount'
import {useNotifications} from '@/composables/useNotifications'
import {exampleSchool, exampleUser} from '@/data/exampleData'
import {auth} from '@/lib/auth'
import {initials} from '@/lib/news'
import type {Action} from '@/lib/permissions'

interface NavItem {
  label: string
  routeName: string
  icon: Component
  count?: number
  // Märket följer antalet olästa notiser i stället för exempelsiffran.
  unread?: boolean
  // Märket visar antalet inlämningar som väntar på bedömning i lärarens kurser.
  toGrade?: boolean
  // Åtgärden som krävs för att länken ska visas.
  requires?: Action
  // Bara elever i någon kurs ser länken.
  studentOnly?: boolean
}

const mainNav: NavItem[] = [
  {label: 'Idag', routeName: 'today', icon: PhHouse},
  {label: 'Kalender', routeName: 'calendar', icon: PhCalendarBlank},
  {label: 'Inkorg', routeName: 'inbox', icon: PhTray, unread: true},
  {label: 'Material', routeName: 'material', icon: PhFolder},
  {label: 'Studieplan', routeName: 'grades', icon: PhGraduationCap, studentOnly: true},
  {label: 'Att bedöma', routeName: 'grading', icon: PhListChecks, toGrade: true, requires: 'grade'},
  {label: 'Inställningar', routeName: 'settings', icon: PhGearSix},
]

const roleLabels: Record<string, string> = {Admin: 'Admin', Teacher: 'Lärare', Student: 'Elev'}

const route = useRoute()
const router = useRouter()
const {courses} = useCourses()
const {me, can, resetCurrentUser} = useCurrentUser()
const {unreadCount} = useNotifications()
const {gradingCount, loadGradingCount} = useGradingCount()

watch(() => can('grade'), (canGrade) => {
  if (canGrade) {
    loadGradingCount()
  }
}, {immediate: true})

const isStudent = computed(() => (me.value?.courses ?? []).some((course) => course.role === 'Student'))
const navItems = computed(() => mainNav
  .filter((item) =>
    (!item.requires || can(item.requires))
    && (!item.studentOnly || isStudent.value))
  .map((item) => ({
    ...item,
    count: item.unread
      ? unreadCount.value
      : item.toGrade
        ? gradingCount.value
        : item.count,
  })))

const userName = computed(() => me.value?.name ?? exampleUser.name)
const userRole = computed(() => me.value
  ? roleLabels[me.value.role] ?? me.value.role
  : exampleUser.role)
const userInitials = computed(() => me.value
  ? initials(me.value.name)
  : exampleUser.initials)
const schoolName = computed(() => me.value?.school.name ?? exampleSchool.name)

async function signOut() {
  await auth.signOut()
  resetCurrentUser()
  await router.push({name: 'login'})
}

function isCourseActive(courseId: string) {
  return route.params.courseId === courseId
}

// Två kurser med samma titel (omgångar för olika klasser) skiljs åt med kurskoden.
function courseLabel(course: {title: string, code: string}): string {
  return courses.value.filter((other) => other.title === course.title).length > 1
    ? `${course.title} · ${course.code}`
    : course.title
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
          <SidebarMenuItem v-for="item in navItems" :key="item.routeName">
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
              class="bg-primary text-primary-foreground peer-hover/menu-button:text-primary-foreground peer-data-active/menu-button:text-primary-foreground rounded-full"
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
              :tooltip="courseLabel(course)"
              :is-active="isCourseActive(course.id)"
            >
              <RouterLink :to="{name: 'course-content', params: {courseId: course.id}}">
                <span class="bg-primary size-2 shrink-0 rounded-[2px]"/>
                <span class="min-w-0 truncate">{{ courseLabel(course) }}</span>
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
          <DropdownMenu>
            <DropdownMenuTrigger as-child>
              <SidebarMenuButton size="lg" data-test="user-menu">
                <Avatar class="size-8">
                  <AvatarFallback class="text-xs font-semibold">{{ userInitials }}</AvatarFallback>
                </Avatar>
                <span class="flex min-w-0 flex-col leading-tight">
                  <span class="truncate font-medium">{{ userName }}</span>
                  <span class="text-muted-foreground truncate text-xs">{{ userRole }} · {{ schoolName }}</span>
                </span>
                <PhCaretUpDown class="text-muted-foreground"/>
              </SidebarMenuButton>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" class="w-(--reka-dropdown-menu-trigger-width) min-w-56">
              <DropdownMenuItem data-test="sign-out" @select="signOut">
                <PhSignOut/>
                Logga ut
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </SidebarMenuItem>
      </SidebarMenu>
    </SidebarFooter>

    <SidebarRail/>
  </Sidebar>
</template>
