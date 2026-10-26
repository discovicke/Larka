<script setup lang="ts">
import {PhCalendarBlank} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import AddModuleDialog from '@/components/course/AddModuleDialog.vue'
import ModuleSection from '@/components/course/ModuleSection.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Empty, EmptyContent, EmptyDescription, EmptyHeader, EmptyMedia, EmptyTitle} from '@/components/ui/empty'
import {Skeleton} from '@/components/ui/skeleton'
import {api, errorMessage, type Schema} from '@/lib/api'

const route = useRoute()
const courseId = computed(() => String(route.params.courseId))

const modules = ref<Schema<'ModuleWithContentDto'>[]>([])
const loading = ref(true)
const error = ref('')

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
      />
      <AddModuleDialog :course-id="courseId" @created="load"/>
    </template>
  </div>
</template>
