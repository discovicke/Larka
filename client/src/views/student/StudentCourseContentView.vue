<script setup lang="ts">
import {computed, onMounted, ref, watch} from 'vue'
import {useRoute, useRouter} from 'vue-router'

import StudentCourseOverview from '@/components/student/StudentCourseOverview.vue'
import StudentPartDetail from '@/components/student/StudentPartDetail.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Skeleton} from '@/components/ui/skeleton'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useNoAccess} from '@/composables/useNoAccess'
import {useToast} from '@/composables/useToast'
import {api, errorMessage, type Schema} from '@/lib/api'
import {fetchStudentModules} from '@/lib/modulesApi'
import {openableParts, type StudentFilter} from '@/lib/studentCourse'

// Elevens vy av en kurs: veckor, låsta delar och klarmarkering. Den visas i stället för redigeraren
// på kursens innehållssida för den som inte kan redigera. Servern vet vilka grupper eleven går i,
// så klienten skickar bara kursen.

const props = defineProps<{courseId: string}>()

const route = useRoute()
const router = useRouter()
const {loadMe} = useCurrentUser()
const {setCrumbLabel} = useCrumbLabels()
const {showToast} = useToast()
const {leave} = useNoAccess()
const modules = ref<Schema<'StudentModuleWithContentDto'>[]>([])
const loading = ref(true)
const error = ref('')
const savingLinkId = ref('')
const selectedLinkId = ref('')
const filter = ref<StudentFilter>('all')

// Framtida veckor är låsta skal. Deras eventuella innehåll får inte dyka upp i detaljvyn,
// även om API:t skulle skicka med något.
const availableModules = computed(() => modules.value.filter((module) => module.isAvailable))
const allItems = computed(() => availableModules.value.flatMap((module) => module.content))

const selectedModule = computed(() => availableModules.value.find((module) =>
  module.content.some((item) => item.linkId === selectedLinkId.value)))
const selectedItem = computed(() => selectedModule.value?.content
  .find((item) => item.linkId === selectedLinkId.value) ?? null)

watch(selectedItem, (item) => {
  if (item) {
    setCrumbLabel(`assignment:${item.linkId}`, item.title)
  }
}, {immediate: true})

// Delarna i kursordning, för Föregående och Nästa del.
const orderedParts = computed(() => openableParts(modules.value).map((entry) => entry.part))
const selectedIndex = computed(() => orderedParts.value.findIndex((item) => item.linkId === selectedLinkId.value))
const previousPart = computed(() => selectedIndex.value > 0
  ? orderedParts.value[selectedIndex.value - 1]!
  : null)
const nextPart = computed(() => selectedIndex.value >= 0
  ? orderedParts.value[selectedIndex.value + 1] ?? null
  : null)

watch(() => props.courseId, async () => {
  selectedLinkId.value = ''
  await load()
}, {immediate: true})

onMounted(loadMe)

// Efter en inlämning hämtas veckorna om utan skelett (silent), så att vyn och statusen uppdateras på plats.
async function load(silent = false) {
  if (!silent) {
    loading.value = true
    error.value = ''
  }
  try {
    modules.value = await fetchStudentModules(props.courseId)
  } catch (exception) {
    error.value = errorMessage(exception)
    if (!silent) {
      modules.value = []
      selectedLinkId.value = ''
    }
  } finally {
    if (!silent) {
      loading.value = false
    }
  }
  if (!silent) {
    syncFromQuery()
  }
}

async function setCompleted(linkId: string, completed: boolean) {
  savingLinkId.value = linkId
  error.value = ''
  try {
    const {data} = await api.PUT('/api/courses/{courseId}/links/{linkId}/completion', {
      params: {path: {courseId: props.courseId, linkId}},
      body: {completed},
    })
    if (data) {
      modules.value = modules.value.map((module) => module.id === data.id
        ? data
        : module)
    }
    showToast(completed
      ? 'Delen är klarmarkerad.'
      : 'Klarmarkeringen är borttagen.')
  } catch (exception) {
    error.value = errorMessage(exception)
    await load()
  } finally {
    savingLinkId.value = ''
  }
}

// Den öppna delen bor i adressen (?del=<linkId>), så att notiser och länkar kan peka på den och
// bakåtknappen fungerar. En låst del ignoreras och eleven hamnar i översikten. En del eleven inte ser
// (okänd, utkast, schemalagd eller i en vecka som inte öppnat) leder till Idag med samma toast som andra
// sidor eleven inte får öppna, så att adressen inte avslöjar att delen finns.
const wantedLinkId = computed(() => typeof route.query.del === 'string'
  ? route.query.del
  : '')

watch(wantedLinkId, syncFromQuery)

function syncFromQuery() {
  if (loading.value) {
    return
  }
  const wanted = wantedLinkId.value
  const item = allItems.value.find((candidate) => candidate.linkId === wanted)
  if (item && !item.isLocked) {
    selectedLinkId.value = wanted
    return
  }
  selectedLinkId.value = ''
  if (!wanted || error.value) {
    return
  }
  if (item) {
    setQuery('')
    return
  }
  void leave()
}

function setQuery(linkId: string, method: 'push' | 'replace' = 'replace') {
  const query = {...route.query}
  delete query.del
  const location = {query: linkId
    ? {...query, del: linkId}
    : query}
  void (method === 'push'
    ? router.push(location)
    : router.replace(location))
}

function openPart(item: Schema<'StudentContentItem'>) {
  selectedLinkId.value = item.linkId
  // En låst del visar bara förklaringen och går inte att länka till.
  setQuery(item.isLocked
    ? ''
    : item.linkId, item.isLocked
    ? 'replace'
    : 'push')
}

function closePart() {
  selectedLinkId.value = ''
  const back = window.history.state?.back
  const sameCourseView = typeof back === 'string'
    && new URL(back, window.location.origin).pathname === window.location.pathname
  if (sameCourseView) {
    router.back()
    return
  }
  setQuery('')
}
</script>

<template>
  <div class="flex flex-col gap-6" data-test="student-view">
    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <template v-if="loading">
      <Skeleton v-for="index in 2" :key="index" class="h-32 w-full rounded-xl"/>
    </template>

    <p v-else-if="modules.length === 0 && !error" class="text-muted-foreground text-sm">
      Kursen har inga veckor än.
    </p>

    <StudentPartDetail
      v-else-if="selectedItem && selectedModule"
      :course-id="courseId"
      :part="selectedItem"
      :module="selectedModule"
      :saving-link-id="savingLinkId"
      :previous-part="previousPart"
      :next-part="nextPart"
      @open="openPart"
      @close="closePart"
      @complete="setCompleted"
      @submitted="load(true)"
      @error="error = $event"
    />

    <StudentCourseOverview
      v-else
      v-model:filter="filter"
      :modules="modules"
      :saving-link-id="savingLinkId"
      @complete="setCompleted"
      @open="openPart"
    />
  </div>
</template>
