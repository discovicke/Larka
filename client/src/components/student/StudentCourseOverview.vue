<script setup lang="ts">
import {
  PhCalendarBlank,
  PhCaretDown,
  PhCaretRight,
  PhCheckCircle,
  PhCircle,
  PhEye,
  PhLock,
  PhPaperPlaneTilt,
} from '@phosphor-icons/vue'
import {computed, nextTick, onMounted} from 'vue'

import StatusBadge from '@/components/StatusBadge.vue'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {Item, ItemMedia} from '@/components/ui/item'
import type {Schema} from '@/lib/api'
import {countLabel, formatDate, partOf} from '@/lib/content'
import {
  dueLabel,
  dueStatus,
  gradeScaleLabel,
  isAvailable,
  isFinished,
  isModuleComplete,
  kindOf,
  rowIcon,
  rowIconClass,
  sectionsFor,
  statusHeading,
  statusKey,
  type StudentFilter,
} from '@/lib/studentCourse'

// Översikten: veckorna med sina delar och elevens progress. Datat och sparandet ägs av vyn runtom.

const props = defineProps<{
  modules: Schema<'StudentModuleWithContentDto'>[]
  savingLinkId: string
}>()

const emit = defineEmits<{
  complete: [linkId: string, completed: boolean]
  open: [item: Schema<'StudentContentItem'>]
}>()

const filter = defineModel<StudentFilter>('filter', {required: true})

// Framtida veckor är låsta skal. Deras eventuella innehåll får inte räknas i progress, även om
// API:t skulle skicka med något.
const availableModules = computed(() => props.modules.filter((module) => module.isAvailable))
const allItems = computed(() => availableModules.value.flatMap((module) => module.content))
const visibleModules = computed(() => filter.value === 'all'
  ? props.modules
  : availableModules.value)
const completable = computed(() => allItems.value.filter((item) => item.type !== 'News'))
const completedCount = computed(() => completable.value.filter(isFinished).length)
const todoCount = computed(() => completable.value.length - completedCount.value)
const availableCount = computed(() => allItems.value.filter(isAvailable).length)
const submissionCount = computed(() => allItems.value.filter((item) => kindOf(item) === 'Inlämning').length)
const gradedCount = computed(() => allItems.value.filter((item) => item.submissionStatus === 'Graded').length)
const lockedCount = computed(() => allItems.value.filter((item) => item.isLocked && !isFinished(item)).length)
const activeModuleId = computed(() => availableModules.value
  .find((module) => !isModuleComplete(module))?.id ?? null)
const progressPercent = computed(() => completable.value.length === 0
  ? 0
  : Math.round((completedCount.value / completable.value.length) * 100))

// Veckan som pågår (den första som inte är klar) är den enda som startar öppen. Den får ett märke,
// och sidan scrollar dit när den öppnas.
const currentId = activeModuleId

onMounted(async () => {
  await nextTick()
  if (currentId.value && availableModules.value.length > 1) {
    document.querySelector(`[data-module-id="${currentId.value}"]`)?.scrollIntoView?.({block: 'start'})
  }
})

const filters: {value: StudentFilter, label: string}[] = [
  {value: 'all', label: 'Allt'},
  {value: 'todo', label: 'Att göra nu'},
  {value: 'submissions', label: 'Inlämningar'},
]

function filterLabel(value: StudentFilter): string {
  if (value === 'todo') {
    return `Att göra nu (${availableCount.value})`
  }
  if (value === 'submissions') {
    return `Inlämningar (${submissionCount.value})`
  }
  return 'Allt'
}

function moduleProgress(module: Schema<'StudentModuleWithContentDto'>): {done: number, total: number} {
  const parts = module.content.filter((item) => item.type !== 'News')
  return {
    done: parts.filter(isFinished).length,
    total: parts.length,
  }
}

function moduleDefaultOpen(module: Schema<'StudentModuleWithContentDto'>): boolean {
  return module.id === activeModuleId.value
}

function detailsOf(item: Schema<'StudentContentItem'>): string {
  const scale = gradeScaleLabel(item.gradeScale)
  return scale
    ? `${kindOf(item)} · ${scale}`
    : kindOf(item)
}

// Märket i raden bara för uppgifter med inlämning, och inte för Att göra eller Låst: de visas av ikonen.
function rowBadge(item: Schema<'StudentContentItem'>) {
  const key = statusKey(item)
  return item.type === 'Assignment' && !item.canMarkDone && key !== 'progress.todo' && key !== 'progress.locked'
    ? key
    : null
}
</script>

<template>
  <div class="flex flex-wrap items-center justify-between gap-3">
    <div role="tablist" class="bg-muted flex w-fit rounded-[7px] p-[3px]">
      <Button
        v-for="option in filters"
        :key="option.value"
        type="button"
        role="tab"
        size="sm"
        variant="ghost"
        :aria-selected="filter === option.value"
        :class="filter === option.value
          ? 'bg-background h-auto rounded-[5px] px-2.5 py-1 text-[13px] font-medium shadow-sm'
          : 'text-muted-foreground h-auto rounded-[5px] px-2.5 py-1 text-[13px]'"
        :data-test="`student-filter-${option.value}`"
        @click="filter = option.value"
      >
        {{ filterLabel(option.value) }}
      </Button>
    </div>
    <div class="text-muted-foreground flex flex-wrap items-center gap-3 text-[13px]">
      <span class="flex items-center gap-1"><PhEye class="size-3.5"/>Teori</span>
      <span class="flex items-center gap-1"><PhPaperPlaneTilt class="size-3.5"/>Inlämning</span>
      <span class="flex items-center gap-1"><PhLock class="size-3.5"/>Låst</span>
    </div>
  </div>

  <div class="flex flex-wrap items-start gap-6">
    <div class="flex min-w-0 flex-[999_1_560px] flex-col gap-4">
      <template v-for="module in visibleModules" :key="module.id">
        <Collapsible
          v-if="module.isAvailable"
          :default-open="moduleDefaultOpen(module)"
          as="section"
          :aria-label="module.title"
          :data-module-id="module.id"
          class="bg-card scroll-mt-4 text-card-foreground overflow-hidden rounded-[11px] border"
        >
          <CollapsibleTrigger
            class="group bg-muted/50 hover:bg-muted/70 flex w-full items-center gap-3 px-4 py-[14px] text-left transition-colors"
            data-test="student-module"
          >
            <PhCaretDown
              class="text-muted-foreground size-4 shrink-0 transition-transform duration-200 group-data-[state=closed]:-rotate-90"
            />
            <span class="flex min-w-0 flex-1 flex-col gap-0.5">
              <span class="truncate font-sans text-[15px] font-semibold">{{ module.title }}</span>
              <span class="text-muted-foreground text-[13px]">
                {{ moduleProgress(module).done }} av {{ moduleProgress(module).total }} klara
                <template v-if="module.description"> · {{ module.description }}</template>
              </span>
            </span>
            <Badge v-if="module.id === currentId && availableModules.length > 1" variant="secondary" data-test="student-module-current">
              Den här veckan
            </Badge>
            <StatusBadge
              v-if="moduleProgress(module).total > 0
                && moduleProgress(module).done === moduleProgress(module).total"
              status="progress.done"
            />
          </CollapsibleTrigger>
          <CollapsibleContent>
            <Collapsible
              v-for="section in sectionsFor(module, filter)"
              :key="section.key"
              :default-open="section.defaultOpen"
              as="section"
              class="border-t first:border-t-0"
              :aria-label="`${section.title} i ${module.title}`"
            >
              <CollapsibleTrigger
                class="group hover:bg-muted/30 flex w-full items-center gap-2 py-2 pr-4 pl-9 text-left transition-colors"
                :data-test="`student-section-${section.key}`"
              >
                <PhCaretDown
                  class="text-muted-foreground size-3.5 shrink-0 transition-transform duration-200 group-data-[state=closed]:-rotate-90"
                />
                <span class="text-muted-foreground min-w-0 flex-1 truncate text-[11px] font-semibold tracking-[0.08em] uppercase">
                  {{ section.title }}
                </span>
                <span class="text-muted-foreground ml-auto text-[11px]">
                  {{ countLabel(section.items.length, 'del', 'delar') }}
                </span>
              </CollapsibleTrigger>
              <CollapsibleContent>
                <p
                  v-if="section.items.length === 0"
                  class="text-muted-foreground border-t py-3 pr-4 pl-12 text-sm"
                >
                  {{ section.empty }}
                </p>

                <div
                  v-for="item in section.items"
                  :key="item.linkId"
                  class="hover:bg-muted/30 flex items-center gap-2 border-t py-2.5 pr-4 pl-12"
                  :data-test="`student-part-${item.linkId}`"
                >
                  <Button
                    v-if="item.canMarkDone && !item.isLocked"
                    variant="ghost"
                    size="icon-sm"
                    class="-ml-1.5 shrink-0"
                    :aria-label="item.isCompleted
                      ? 'Ångra klarmarkering'
                      : 'Klarmarkera'"
                    :data-test="`${item.isCompleted
                      ? 'undo'
                      : 'complete'}-${item.linkId}`"
                    :disabled="savingLinkId === item.linkId"
                    @click.stop="emit('complete', item.linkId, !item.isCompleted)"
                  >
                    <PhCheckCircle v-if="item.isCompleted" class="size-[18px] text-success"/>
                    <PhCircle v-else class="size-[18px]"/>
                  </Button>
                  <component
                    v-else
                    :is="rowIcon(item)"
                    class="size-[18px] shrink-0"
                    :class="[rowIconClass(item), rowBadge(item)
                      ? 'sm:invisible'
                      : '']"
                    :aria-label="statusHeading(item)"
                    role="img"
                  />
                  <button
                    type="button"
                    class="flex min-w-0 flex-1 items-center gap-3 text-left"
                    :data-test="`student-open-${item.linkId}`"
                    @click="emit('open', item)"
                  >
                    <component :is="partOf(item).icon" class="text-muted-foreground size-4 shrink-0"/>
                    <span class="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span
                        class="text-sm break-words"
                        :class="item.isLocked
                          ? 'text-muted-foreground'
                          : 'font-medium'"
                      >
                        {{ item.title }}
                      </span>
                      <span class="text-muted-foreground text-[12px]">{{ detailsOf(item) }}</span>
                    </span>
                    <span class="hidden items-center gap-2 sm:flex">
                      <StatusBadge v-if="rowBadge(item)" :status="rowBadge(item)!"/>
                      <StatusBadge v-if="dueStatus(item) && !item.isLocked" :status="dueStatus(item)!"/>
                      <span
                        v-if="item.dueAt && !item.isLocked"
                        class="text-muted-foreground text-[12px] whitespace-nowrap"
                      >
                        {{ dueLabel(item) }}
                      </span>
                    </span>
                    <PhCaretRight class="text-muted-foreground size-4 shrink-0"/>
                  </button>
                </div>
              </CollapsibleContent>
            </Collapsible>
          </CollapsibleContent>
        </Collapsible>

        <Item
          v-else
          as="section"
          :aria-label="module.title"
          variant="outline"
          size="sm"
          class="bg-card rounded-[11px] px-4 py-[14px]"
          data-test="student-module-locked"
        >
          <ItemMedia variant="icon" class="text-muted-foreground">
            <PhLock/>
          </ItemMedia>
          <span class="flex min-w-0 flex-1 flex-col gap-0.5">
            <span class="truncate font-sans text-[15px] font-semibold">{{ module.title }}</span>
            <span class="text-muted-foreground text-[13px]">Veckan är låst</span>
          </span>
          <span class="text-muted-foreground flex items-center gap-1.5 text-[13px]">
            <PhCalendarBlank class="size-4"/>
            Öppnar {{ module.publishAt
              ? formatDate(module.publishAt, true)
              : 'senare' }}
          </span>
        </Item>
      </template>
    </div>

    <aside class="flex min-w-0 flex-[1_1_280px] flex-col gap-4">
      <section class="bg-card flex flex-col gap-2.5 rounded-[11px] border p-4">
        <h2 class="text-sm font-semibold">Din progress</h2>
        <div class="flex items-baseline gap-1.5">
          <span class="text-[28px] font-semibold tracking-tight">{{ completedCount }}</span>
          <span class="text-muted-foreground text-sm">av {{ completable.length }} delar klara</span>
        </div>
        <div class="bg-muted h-1.5 overflow-hidden rounded-full">
          <div
            class="bg-primary h-full rounded-full transition-all"
            :style="{width: `${progressPercent}%`}"
          />
        </div>
        <dl class="flex flex-col gap-1.5 pt-1 text-[13px]">
          <div class="flex items-center justify-between gap-2">
            <dt class="text-muted-foreground">Tillgängligt nu</dt>
            <dd>{{ availableCount }}</dd>
          </div>
          <div class="flex items-center justify-between gap-2">
            <dt class="text-muted-foreground">Betygsatta delar</dt>
            <dd>{{ gradedCount }}</dd>
          </div>
          <div class="flex items-center justify-between gap-2">
            <dt class="text-muted-foreground">Låsta delar</dt>
            <dd>{{ lockedCount }}</dd>
          </div>
          <div class="flex items-center justify-between gap-2">
            <dt class="text-muted-foreground">Kvar totalt</dt>
            <dd>{{ todoCount }}</dd>
          </div>
        </dl>
      </section>
    </aside>
  </div>
</template>
