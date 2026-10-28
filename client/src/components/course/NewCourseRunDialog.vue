<script setup lang="ts">
import {computed, ref, watch} from 'vue'
import {useRouter} from 'vue-router'

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
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useCourses} from '@/composables/useCourses'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, errorMessage, type Schema} from '@/lib/api'
import {countLabel} from '@/lib/content'

const props = defineProps<{course: Schema<'CourseDto'>}>()

const open = defineModel<boolean>('open', {required: true})

const router = useRouter()
const {loadCourses, setPendingNotice} = useCourses()
const {refreshMe} = useCurrentUser()

const code = ref('')
const groupId = ref('')
const startsOn = ref('')
const endsOn = ref('')
const copyContent = ref(true)
const classes = ref<Schema<'GroupDto'>[]>([])
const weeks = ref<Schema<'ModuleWithContentDto'>[]>([])
const loading = ref(false)
const saving = ref(false)
const error = ref('')

const partTotal = computed(() => weeks.value.reduce((sum, week) => sum + week.content.length, 0))
const copyLabel = computed(() => `Kopiera alla veckor och delar (${
  countLabel(weeks.value.length, 'vecka', 'veckor')}, ${countLabel(partTotal.value, 'del', 'delar')})`)
const chosenClass = computed(() => classes.value.find((found) => found.id === groupId.value))
const ready = computed(() => code.value.trim() !== ''
  && groupId.value !== ''
  && startsOn.value !== ''
  && endsOn.value !== '')

watch(open, async (isOpen) => {
  if (!isOpen) {
    return
  }

  code.value = ''
  groupId.value = ''
  startsOn.value = ''
  endsOn.value = ''
  copyContent.value = true
  error.value = ''
  loading.value = true
  try {
    const [groups, modules] = await Promise.all([
      api.GET('/api/groups'),
      api.GET('/api/courses/{id}/modules', {params: {path: {id: props.course.id}}}),
    ])
    // Bara klasser i kursens skola kan läsa kursen.
    classes.value = (groups.data ?? []).filter((group) => group.type === 'Class'
      && group.schoolId === props.course.schoolId)
    weeks.value = modules.data ?? []
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    loading.value = false
  }
})

async function create() {
  if (!ready.value) {
    return
  }

  saving.value = true
  error.value = ''
  try {
    const {data} = await api.POST('/api/courses/{id}/runs', {
      params: {path: {id: props.course.id}},
      body: {
        code: code.value.trim(),
        groupId: groupId.value,
        startsOn: startsOn.value,
        endsOn: endsOn.value,
        copyContent: copyContent.value,
      },
    })
    if (!data) {
      return
    }

    // Stäng först: när kurserna hämtas om byggs kurssidan om, och en öppen dialog skulle ligga kvar.
    const notice = `Kursomgången skapades för ${chosenClass.value?.name ?? 'klassen'}. ${
      countLabel(data.moduleCount, 'vecka', 'veckor')} och ${
      countLabel(data.partCount, 'del', 'delar')} kopierades som utkast.`
    open.value = false
    // Kursen och lärarrollen i den är nya, så båda listorna måste hämtas om innan sidan öppnas.
    await Promise.all([loadCourses(), refreshMe()])
    setPendingNotice(data.courseId, notice)
    await router.push({name: 'course-content', params: {courseId: data.courseId}})
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="grid-cols-[minmax(0,1fr)] sm:max-w-lg">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="create">
        <DialogHeader class="min-w-0">
          <DialogTitle class="break-words">Ny kursomgång av {{ course.title }}</DialogTitle>
          <DialogDescription>
            Kursen skapas med samma beskrivning och omfattning, och lärarna följer med till klassen.
          </DialogDescription>
        </DialogHeader>

        <div class="grid min-w-0 grid-cols-2 gap-3">
          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="run-title">Titel</FieldLabel>
            <p
              id="run-title"
              class="bg-muted/50 flex h-8 min-w-0 items-center rounded-lg border px-2.5 text-sm"
              data-test="run-title"
            >
              <span class="truncate">{{ course.title }}</span>
            </p>
          </div>
          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="run-code">Kurskod</FieldLabel>
            <Input
              id="run-code"
              v-model="code"
              class="w-full"
              placeholder="NET26-DEVOPS"
              required
              data-test="run-code"
            />
          </div>
        </div>

        <div class="flex min-w-0 flex-col gap-1.5">
          <FieldLabel for="run-class">Klass</FieldLabel>
          <NativeSelect
            id="run-class"
            v-model="groupId"
            class="w-full"
            required
            data-test="run-class"
          >
            <NativeSelectOption value="">Välj klass…</NativeSelectOption>
            <NativeSelectOption v-for="group in classes" :key="group.id" :value="group.id">
              {{ group.name }}{{ group.term
                ? ` · ${group.term}`
                : '' }}
            </NativeSelectOption>
          </NativeSelect>
        </div>

        <div class="grid min-w-0 grid-cols-2 gap-3">
          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="run-starts">Startar</FieldLabel>
            <Input
              id="run-starts"
              v-model="startsOn"
              type="date"
              class="w-full"
              required
              data-test="run-starts"
            />
          </div>
          <div class="flex min-w-0 flex-col gap-1.5">
            <FieldLabel for="run-ends">Slutar</FieldLabel>
            <Input
              id="run-ends"
              v-model="endsOn"
              type="date"
              class="w-full"
              required
              data-test="run-ends"
            />
          </div>
        </div>

        <FieldLabel class="w-full min-w-0 cursor-pointer items-start gap-3 font-normal">
          <Checkbox
            :model-value="copyContent"
            :disabled="loading"
            data-test="run-copy"
            @update:model-value="(checked) => copyContent = checked === true"
          />
          <span class="min-w-0 flex-1 text-sm break-words" data-test="run-copy-label">{{ copyLabel }}</span>
        </FieldLabel>
        <p class="text-muted-foreground text-sm">
          Deadlines flyttas lika mycket som startdatumen skiljer sig åt. Allt startar som utkast.
        </p>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button type="submit" :disabled="saving || !ready" data-test="confirm-run">
            Skapa kursomgång
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
