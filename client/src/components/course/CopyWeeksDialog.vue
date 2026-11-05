<script setup lang="ts">
import {PhCalendarBlank} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {FieldLabel} from '@/components/ui/field'
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {useAsync} from '@/composables/useAsync'
import {api, errorMessage, type Schema} from '@/lib/api'
import {countLabel} from '@/lib/content'
import {periodLabel} from '@/lib/coursePeriod'
import {fetchCourseModules} from '@/lib/modulesApi'

const props = defineProps<{courseId: string}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{copied: [result: Schema<'CopyWeeksResultDto'>]}>()

const {courses} = useCourses()
const {can} = useCurrentUser()

const sourceId = ref('')
const weeks = ref<Schema<'ModuleWithContentDto'>[]>([])
const selected = ref<string[]>([])
const saving = ref(false)
// Samma felruta visar felet från hämtningen och från kopieringen.
const {run: loadWeeks, loading: loadingWeeks, error} = useAsync(fetchCourseModules)

// Kurser läraren kan undervisa i, förutom den som veckorna ska in i.
const sources = computed(() => courses.value
  .filter((course) => course.id !== props.courseId && can('editContent', course.id)))
const allChosen = computed(() => weeks.value.length > 0 && selected.value.length === weeks.value.length)
const chosenWeeks = computed(() => weeks.value.filter((week) => selected.value.includes(week.id)))
const partTotal = computed(() => chosenWeeks.value.reduce((sum, week) => sum + week.content.length, 0))
const preview = computed(() => `${countLabel(chosenWeeks.value.length, 'vecka', 'veckor')}, ${
  countLabel(partTotal.value, 'del', 'delar')} kopieras`)

watch(open, (isOpen) => {
  if (isOpen) {
    sourceId.value = ''
    weeks.value = []
    selected.value = []
    error.value = ''
  }
})

watch(sourceId, async (id) => {
  weeks.value = []
  selected.value = []
  if (!id) {
    return
  }

  const loaded = await loadWeeks(id)
  // Hann läraren byta kurs under tiden är svaret inaktuellt.
  if (loaded && sourceId.value === id) {
    weeks.value = loaded
  }
})

function toggle(weekId: string, checked: boolean) {
  selected.value = checked
    ? [...selected.value, weekId]
    : selected.value.filter((id) => id !== weekId)
}

function toggleAll() {
  selected.value = allChosen.value
    ? []
    : weeks.value.map((week) => week.id)
}

function sourceLabel(course: Schema<'CourseDto'>): string {
  return [course.title, course.code, periodLabel(course.startsOn ?? null, course.endsOn ?? null)]
    .filter((part) => part !== '')
    .join(' · ')
}

async function copy() {
  if (!sourceId.value || selected.value.length === 0) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    const {data} = await api.POST('/api/courses/{id}/modules/copy', {
      params: {path: {id: props.courseId}},
      body: {sourceCourseId: sourceId.value, moduleIds: selected.value},
    })
    open.value = false
    if (data) {
      emit('copied', data)
    }
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)]">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="copy">
        <DialogHeader class="min-w-0">
          <DialogTitle>Kopiera från annan kurs</DialogTitle>
          <DialogDescription>
            Veckorna hamnar sist i kursen som utkast, med egna kopior av delarna, bilagorna och
            beroendena. Datumen flyttas lika mycket som kursernas startdatum skiljer sig åt.
            Inlämningar, betyg och elevernas framsteg följer inte med.
          </DialogDescription>
        </DialogHeader>

        <Select v-model="sourceId">
          <SelectTrigger class="w-full" aria-label="Välj kurs" data-test="copy-source">
            <SelectValue placeholder="Välj kurs…"/>
          </SelectTrigger>
          <SelectContent position="popper">
            <SelectItem v-for="course in sources" :key="course.id" :value="course.id">
              {{ sourceLabel(course) }}
            </SelectItem>
          </SelectContent>
        </Select>
        <p v-if="sources.length === 0" class="text-muted-foreground text-sm">
          Du undervisar inte i någon annan kurs att kopiera från.
        </p>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <div
          v-if="sourceId"
          class="flex flex-col gap-2"
          role="group"
          aria-label="Välj veckor"
        >
          <p v-if="!loadingWeeks && weeks.length === 0" class="text-muted-foreground text-sm">
            Kursen har inga veckor att kopiera.
          </p>
          <div v-else-if="weeks.length > 0" class="flex justify-end">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              data-test="copy-all"
              @click="toggleAll"
            >
              {{ allChosen
                ? 'Avmarkera alla'
                : 'Välj alla' }}
            </Button>
          </div>
          <FieldLabel
            v-for="week in weeks"
            :key="week.id"
            class="has-data-checked:border-primary/60 hover:bg-muted/50 w-full min-w-0 cursor-pointer items-center gap-3 rounded-lg border p-3 font-normal transition-colors"
          >
            <Checkbox
              :model-value="selected.includes(week.id)"
              :data-test="`copy-week-${week.id}`"
              @update:model-value="(checked) => toggle(week.id, checked === true)"
            />
            <PhCalendarBlank class="text-muted-foreground size-4 shrink-0"/>
            <span class="min-w-0 flex-1">
              <span class="block text-sm font-medium break-words">{{ week.title }}</span>
              <span class="text-muted-foreground text-xs">
                {{ countLabel(week.content.length, 'del', 'delar') }}
              </span>
            </span>
          </FieldLabel>
        </div>

        <p
          v-if="selected.length > 0"
          class="text-muted-foreground text-sm"
          data-test="copy-preview"
        >
          {{ preview }}
        </p>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button type="submit" :disabled="saving || selected.length === 0" data-test="confirm-copy">
            Kopiera
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
