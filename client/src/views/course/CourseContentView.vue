<script setup lang="ts">
import {PhCalendarBlank} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import AddModuleDialog from '@/components/course/AddModuleDialog.vue'
import ModuleSection from '@/components/course/ModuleSection.vue'
import RoadmapDialog from '@/components/course/RoadmapDialog.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {api, errorMessage, type Schema} from '@/lib/api'

const route = useRoute()
const courseId = computed(() => String(route.params.courseId))

const modules = ref<Schema<'ModuleWithContentDto'>[]>([])
const loading = ref(true)
const error = ref('')
const deleteOpen = ref(false)
const partToDelete = ref<Schema<'ModuleContentItem'> | null>(null)
const deleting = ref(false)
const deleteError = ref('')
const roadmapOpen = ref(false)
const roadmapModule = ref<Schema<'ModuleWithContentDto'> | null>(null)

async function load() {
  error.value = ''
  try {
    const {data} = await api.GET('/api/courses/{id}/modules', {params: {path: {id: courseId.value}}})
    modules.value = data ?? []
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
}

async function move(index: number, direction: -1 | 1) {
  const moduleIds = modules.value.map((module) => module.id)
  const [moved] = moduleIds.splice(index, 1)
  moduleIds.splice(index + direction, 0, moved)
  try {
    await api.PUT('/api/courses/{id}/modules/order', {params: {path: {id: courseId.value}}, body: {moduleIds}})
    await load()
  } catch (exception) {
    error.value = errorMessage(exception)
  }
}

async function movePart(
  module: Schema<'ModuleWithContentDto'>,
  item: Schema<'ModuleContentItem'>,
  direction: -1 | 1,
) {
  const linkIds = module.content.map((part) => part.linkId)
  const index = linkIds.indexOf(item.linkId)
  if (index < 0) {
    return
  }

  const [moved] = linkIds.splice(index, 1)
  linkIds.splice(index + direction, 0, moved)
  try {
    await api.PUT('/api/courses/{id}/modules/{moduleId}/content/order', {
      params: {path: {id: courseId.value, moduleId: module.id}},
      body: {linkIds},
    })
    await load()
  } catch (exception) {
    error.value = errorMessage(exception)
  }
}

async function togglePartStatus(item: Schema<'ModuleContentItem'>) {
  const status = item.status === 'Draft'
    ? 'Published'
    : 'Draft'
  try {
    await api.PUT('/api/content/{id}/status', {
      params: {path: {id: item.contentId}},
      body: {status},
    })
    await load()
  } catch (exception) {
    error.value = errorMessage(exception)
  }
}

function askDeletePart(item: Schema<'ModuleContentItem'>) {
  partToDelete.value = item
  deleteError.value = ''
  deleteOpen.value = true
}

function openRoadmap(module: Schema<'ModuleWithContentDto'>) {
  roadmapModule.value = module
  roadmapOpen.value = true
}

function applyRoadmap(saved: Schema<'ModuleWithContentDto'>) {
  modules.value = modules.value.map((module) => module.id === saved.id
    ? saved
    : module)
}

async function deletePart() {
  const part = partToDelete.value
  if (!part) {
    return
  }

  deleting.value = true
  deleteError.value = ''
  try {
    await api.DELETE('/api/content/{id}/links/{linkId}', {
      params: {path: {id: part.contentId, linkId: part.linkId}},
    })
    deleteOpen.value = false
    partToDelete.value = null
    await load()
  } catch (exception) {
    deleteError.value = errorMessage(exception)
  } finally {
    deleting.value = false
  }
}

watch(courseId, () => {
  loading.value = true
  load()
}, {immediate: true})
</script>

<template>
  <div class="flex flex-col gap-4">
    <Alert v-if="error" variant="destructive">
      <AlertDescription>{{ error }}</AlertDescription>
    </Alert>

    <template v-if="loading">
      <Skeleton v-for="i in 2" :key="i" class="h-32 w-full rounded-[11px]"/>
    </template>

    <Empty v-else-if="modules.length === 0 && !error" class="border border-dashed">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <PhCalendarBlank/>
        </EmptyMedia>
        <EmptyTitle>Inga veckor än</EmptyTitle>
        <EmptyDescription>Dela in kursen i veckor eller avsnitt och lägg till delar i dem.</EmptyDescription>
      </EmptyHeader>
      <EmptyContent>
        <AddModuleDialog :course-id="courseId" @created="load"/>
      </EmptyContent>
    </Empty>

    <template v-else>
      <ModuleSection
        v-for="(module, index) in modules"
        :key="module.id"
        :module="module"
        :can-move-up="index > 0"
        :can-move-down="index < modules.length - 1"
        @move="(direction) => move(index, direction)"
        @move-part="(item, direction) => movePart(module, item, direction)"
        @toggle-part-status="togglePartStatus"
        @delete-part="askDeletePart"
        @open-roadmap="openRoadmap(module)"
      />
      <RoadmapDialog
        v-if="roadmapModule"
        v-model:open="roadmapOpen"
        :module="roadmapModule"
        :course-id="courseId"
        @saved="applyRoadmap"
      />
      <AddModuleDialog :course-id="courseId" @created="load"/>
    </template>

    <Dialog v-model:open="deleteOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ta bort del?</DialogTitle>
          <DialogDescription>
            {{ partToDelete?.title }} tas bort från veckan. Det går inte att ångra.
          </DialogDescription>
        </DialogHeader>
        <Alert v-if="deleteError" variant="destructive">
          <AlertDescription>{{ deleteError }}</AlertDescription>
        </Alert>
        <DialogFooter>
          <Button variant="outline" :disabled="deleting" @click="deleteOpen = false">
            Avbryt
          </Button>
          <Button variant="destructive" :disabled="deleting" @click="deletePart">
            Ta bort
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
