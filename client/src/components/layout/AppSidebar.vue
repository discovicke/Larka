<script setup lang="ts">
import {
  PhCalendarBlank,
  PhCaretDown,
  PhCaretUpDown,
  PhCheck,
  PhFolder,
  PhGearSix,
  PhGraduationCap,
  PhHouse,
  PhListChecks,
  PhSignOut,
  PhTray,
  PhUserList,
  PhUsersThree,
} from '@phosphor-icons/vue'
import {type Component, computed, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import AppLogo from '@/components/layout/AppLogo.vue'
import {Avatar, AvatarFallback, AvatarImage} from '@/components/ui/avatar'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
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
  useSidebar,
} from '@/components/ui/sidebar'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useGradingCount} from '@/composables/useGradingCount'
import {useNotifications} from '@/composables/useNotifications'
import {exampleUser} from '@/data/exampleData'
import {auth} from '@/lib/auth'
import {courseStatus, splitFinished} from '@/lib/courseStatus'
import {initials} from '@/lib/person'
import type {Action} from '@/lib/permissions'
import {userRoleLabels} from '@/lib/userRoles'

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
]

const route = useRoute()
const router = useRouter()
const {courses} = useCourses()
const {me, can, resetCurrentUser} = useCurrentUser()
const {unreadCount} = useNotifications()
const {gradingCount, loadGradingCount} = useGradingCount()

// På telefonen ligger menyn över sidan. Den stängs när man har gått någonstans, så att den nya sidan syns direkt.
const {setOpenMobile} = useSidebar()
watch(() => route.fullPath, () => {
  setOpenMobile(false)
})

watch(() => can('grade'), (canGrade) => {
  if (canGrade) {
    loadGradingCount()
  }
}, {immediate: true})

// Admin ser skolans alla kurser och övriga sina egna.
const coursesLabel = computed(() => me.value?.role === 'Admin'
  ? 'Skolans kurser'
  : 'Mina kurser')

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

const roleLabels: Record<string, string> = userRoleLabels

const userName = computed(() => me.value?.name ?? exampleUser.name)
const userRole = computed(() => me.value
  ? roleLabels[me.value.role] ?? me.value.role
  : exampleUser.role)
const userInitials = computed(() => me.value
  ? initials(me.value.name)
  : exampleUser.initials)
const schoolName = computed(() => me.value?.school.name ?? '')

// Logga ut slutar alltid på en ren /login, utan redirect: nästa person på samma dator ska inte hamna på
// förra användarens sida (#519). Användaren nollställs först när sidan är lämnad, så att den gamla sidan
// inte hinner hämta något på nytt.
async function signOut() {
  await auth.signOut()
  await router.replace({name: 'login'})
  resetCurrentUser()
}

type SidebarCourse = (typeof courses.value)[number]

// Pågående kurser (och kurser utan datum) först, sedan kommande efter startdatum.
// Avslutade hamnar i en hopfälld grupp, de nyast avslutade först.
const split = computed(() => splitFinished(courses.value))
const currentCourses = computed(() => {
  const running = split.value.current.filter((course) => courseStatus(course) !== 'upcoming')
  const upcoming = split.value.current
    .filter((course) => courseStatus(course) === 'upcoming')
    .sort((first, second) => (first.startsOn ?? '').localeCompare(second.startsOn ?? ''))
  return [...running, ...upcoming]
})
const finishedCourses = computed(() => [...split.value.finished]
  .sort((first, second) => (second.endsOn ?? '').localeCompare(first.endsOn ?? '')))

// Gruppen är hopfälld, men fälls ut när man står på en avslutad kurs så att den syns i menyn.
const finishedOpen = ref(false)
watch(() => [route.params.courseId, finishedCourses.value] as const, ([courseId]) => {
  if (finishedCourses.value.some((course) => course.id === courseId)) {
    finishedOpen.value = true
  }
}, {immediate: true})

function isCourseActive(courseId: string) {
  return route.params.courseId === courseId
}

// Två kurser med samma titel (omgångar för olika klasser) skiljs åt med kurskoden.
function courseLabel(course: SidebarCourse): string {
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
              <SidebarMenuButton
                size="lg"
                tooltip="Byt skola"
                data-test="school-switcher"
                class="group-data-[collapsible=icon]:p-2!"
              >
                <AppLogo/>
                <span class="flex flex-1 flex-col leading-tight group-data-[collapsible=icon]:hidden">
                  <span class="font-semibold">Lärka</span>
                  <span class="text-sidebar-foreground/70 text-xs">{{ schoolName }}</span>
                </span>
                <PhCaretUpDown class="text-sidebar-foreground/70 group-data-[collapsible=icon]:hidden"/>
              </SidebarMenuButton>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" class="w-(--reka-dropdown-menu-trigger-width) min-w-56">
              <DropdownMenuLabel class="text-muted-foreground text-xs">Skola</DropdownMenuLabel>
              <DropdownMenuItem>
                {{ schoolName }}
                <PhCheck class="ml-auto"/>
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
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
              class="rounded-full"
              :class="item.unread
                ? 'bg-info text-white dark:text-background peer-hover/menu-button:text-white dark:peer-hover/menu-button:text-background peer-data-active/menu-button:text-white dark:peer-data-active/menu-button:text-background'
                : 'bg-primary text-primary-foreground peer-hover/menu-button:text-primary-foreground peer-data-active/menu-button:text-primary-foreground'"
            >
              {{ item.count }}
            </SidebarMenuBadge>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarGroup>

      <SidebarGroup>
        <SidebarGroupLabel>{{ coursesLabel }}</SidebarGroupLabel>
        <SidebarMenu>
          <SidebarMenuItem v-for="course in currentCourses" :key="course.id">
            <SidebarMenuButton
              as-child
              :tooltip="courseLabel(course)"
              :is-active="isCourseActive(course.id)"
              class="group-data-[collapsible=icon]:justify-center"
            >
              <RouterLink :to="{name: 'course-content', params: {courseId: course.id}}">
                <span class="bg-primary size-2 shrink-0 rounded-[2px]"/>
                <span class="truncate group-data-[collapsible=icon]:hidden">{{ courseLabel(course) }}</span>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarGroup>

      <Collapsible v-if="finishedCourses.length" v-model:open="finishedOpen" as-child>
        <SidebarGroup data-test="finished-courses">
          <CollapsibleTrigger as-child>
            <SidebarGroupLabel as="button" class="hover:bg-sidebar-foreground/10 w-full cursor-pointer gap-1.5 group-data-[collapsible=icon]:justify-center">
              <span class="flex-1 text-left">Avslutade</span>
              <PhCaretDown class="transition-transform" :class="{'-rotate-90': !finishedOpen}"/>
            </SidebarGroupLabel>
          </CollapsibleTrigger>
          <CollapsibleContent>
            <SidebarMenu>
              <SidebarMenuItem v-for="course in finishedCourses" :key="course.id">
                <SidebarMenuButton
                  as-child
                  :tooltip="courseLabel(course)"
                  :is-active="isCourseActive(course.id)"
                  class="group-data-[collapsible=icon]:justify-center"
                >
                  <RouterLink :to="{name: 'course-content', params: {courseId: course.id}}">
                    <span class="bg-primary size-2 shrink-0 rounded-[2px]"/>
                    <span class="truncate group-data-[collapsible=icon]:hidden">{{ courseLabel(course) }}</span>
                  </RouterLink>
                </SidebarMenuButton>
              </SidebarMenuItem>
            </SidebarMenu>
          </CollapsibleContent>
        </SidebarGroup>
      </Collapsible>

      <SidebarGroup>
        <SidebarGroupLabel>Skola</SidebarGroupLabel>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              as-child
              tooltip="Alla grupper"
              :is-active="route.name === 'groups' || route.name === 'group'"
            >
              <RouterLink :to="{name: 'groups'}">
                <PhUsersThree/>
                <span>Alla grupper</span>
              </RouterLink>
            </SidebarMenuButton>
          </SidebarMenuItem>
          <SidebarMenuItem v-if="can('admin')">
            <SidebarMenuButton as-child tooltip="Användare" :is-active="route.name === 'users'">
              <RouterLink :to="{name: 'users'}" data-test="users-link">
                <PhUserList/>
                <span>Användare</span>
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
              <!-- Samma vänsterkant öppen och ihopfälld, så att avataren inte hoppar när menyn animeras. -->
              <SidebarMenuButton size="lg" data-test="user-menu" class="pl-0">
                <Avatar class="size-8">
                  <AvatarImage
                    v-if="me?.avatarUrl"
                    :src="me?.avatarUrl"
                    alt=""
                    data-test="user-avatar-image"
                  />
                  <AvatarFallback class="text-xs font-semibold">{{ userInitials }}</AvatarFallback>
                </Avatar>
                <span class="flex min-w-0 flex-1 flex-col leading-tight group-data-[collapsible=icon]:hidden">
                  <span class="truncate font-medium">{{ userName }}</span>
                  <span class="text-sidebar-foreground/70 truncate text-xs">{{ userRole }} · {{ schoolName }}</span>
                </span>
                <PhCaretUpDown class="text-sidebar-foreground/70 group-data-[collapsible=icon]:hidden"/>
              </SidebarMenuButton>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" class="w-(--reka-dropdown-menu-trigger-width) min-w-56">
              <DropdownMenuItem data-test="settings-link" @select="router.push({name: 'settings'})">
                <PhGearSix/>
                Inställningar
              </DropdownMenuItem>
              <DropdownMenuSeparator/>
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
