<script setup lang="ts">
import {PhArrowDown, PhEye, PhEyeSlash, PhLock, PhLockOpen, PhX} from '@phosphor-icons/vue'
import {computed, reactive, ref, watch} from 'vue'

import StatusBadge from '@/components/StatusBadge.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Badge} from '@/components/ui/badge'
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
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import {useAsync} from '@/composables/useAsync'
import {api, type Schema} from '@/lib/api'
import {countLabel, formatDate, partOf} from '@/lib/content'
import {
  cycleLabel,
  findCycle,
  nodeOf,
  previewState,
  stageHint,
  stageLabel,
  stagesOf,
  unlocksOf,
  type PreviewState,
  type RoadmapNode,
} from '@/lib/roadmap'

const props = defineProps<{
  module: Schema<'ModuleWithContentDto'>
  courseId: string
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{saved: [module: Schema<'ModuleWithContentDto'>]}>()

const preview = ref(false)
const blockedBy = ref<Record<string, string[]>>({})
const rules = ref<Record<string, Schema<'CompletionRule'> | null>>({})
const picked = reactive<Record<string, string>>({})

const nodes = computed<RoadmapNode[]>(() => props.module.content.map((part) => ({
  ...nodeOf(part),
  blockedBy: blockedBy.value[part.linkId] ?? [],
})))

const nodesById = computed(() => new Map(nodes.value.map((node) => [node.linkId, node])))
const partsById = computed(() => new Map(props.module.content.map((part) => [part.linkId, part])))
const cycle = computed(() => findCycle(nodes.value))
const cycleText = computed(() => cycle.value
  ? cycleLabel(cycle.value, nodes.value)
  : '')
const usedAsBlocker = computed(() => new Set(nodes.value.flatMap((node) => node.blockedBy)))
const stages = computed(() => stagesOf(nodes.value))
const directCount = computed(() => nodes.value.filter((node) =>
  node.type !== 'News' && node.blockedBy.length === 0).length)
const lockedCount = computed(() => nodes.value.filter((node) =>
  node.type !== 'News' && node.blockedBy.length > 0).length)

interface Row {
  part: Schema<'ModuleContentItem'>
  node: RoadmapNode
  blockers: RoadmapNode[]
  unlocks: RoadmapNode[]
  candidates: RoadmapNode[]
  preview: PreviewState
}

const rows = computed<Row[]>(() => nodes.value.map((node) => {
  const part = partsById.value.get(node.linkId)
  if (!part) {
    throw new Error(`Unknown part: ${node.linkId}`)
  }
  return {
    part,
    node,
    blockers: node.blockedBy
      .map((linkId) => nodesById.value.get(linkId))
      .filter((blocker): blocker is RoadmapNode => blocker !== undefined),
    unlocks: unlocksOf(nodes.value, node.linkId),
    candidates: candidatesFor(node),
    // Utkast publiceras när roadmapen sparas, så förhandsvisningen visar den publicerade delen.
    preview: previewState(publishedOnSave(node)),
  }
}))

const rowsByLinkId = computed(() => new Map(rows.value.map((row) => [row.node.linkId, row])))
const stageRows = computed(() => stages.value.map((stage, index) => ({
  index,
  rows: stage.map((node) => rowsByLinkId.value.get(node.linkId)!),
})))

function publishedOnSave(node: RoadmapNode): RoadmapNode {
  return node.status === 'Draft'
    ? {...node, status: 'Published'}
    : node
}

function candidatesFor(node: RoadmapNode): RoadmapNode[] {
  return nodes.value.filter((candidate) =>
    candidate.linkId !== node.linkId
    && candidate.type !== 'News'
    && !node.blockedBy.includes(candidate.linkId))
}

function candidatesIn(stage: RoadmapNode[], node: RoadmapNode): RoadmapNode[] {
  return stage.filter((candidate) =>
    candidate.linkId !== node.linkId
    && candidate.type !== 'News'
    && !node.blockedBy.includes(candidate.linkId))
}

function reset() {
  blockedBy.value = Object.fromEntries(props.module.content.map((part) => [
    part.linkId,
    [...(part.blockedBy ?? [])],
  ]))
  rules.value = Object.fromEntries(props.module.content.map((part) => [
    part.linkId,
    part.completionRule ?? null,
  ]))
  for (const linkId of Object.keys(picked)) {
    delete picked[linkId]
  }
  preview.value = false
  error.value = ''
}

function addBlocker(node: RoadmapNode, value: unknown) {
  const blockerId = String(value ?? '')
  if (!blockerId) {
    return
  }
  const current = blockedBy.value[node.linkId] ?? []
  if (!current.includes(blockerId)) {
    blockedBy.value[node.linkId] = [...current, blockerId]
  }
  picked[node.linkId] = ''
}

function removeBlocker(node: RoadmapNode, blockerId: string) {
  blockedBy.value[node.linkId] = (blockedBy.value[node.linkId] ?? [])
    .filter((candidate) => candidate !== blockerId)
}

function setRule(linkId: string, value: unknown) {
  rules.value[linkId] = value === 'Approved'
    ? 'Approved'
    : 'Submitted'
}

/** En förutsättning som publiceras senare öppnar inte de låsta delarna förrän då. */
function warningOf(row: Row): string {
  if (!usedAsBlocker.value.has(row.node.linkId)) {
    return ''
  }
  if (new Date(row.node.publishAt) > new Date()) {
    return `Publiceras ${formatDate(row.node.publishAt, true)}: de delar som är låsta av den här öppnas först då.`
  }
  return ''
}

function isSubmission(part: Schema<'ModuleContentItem'>): boolean {
  // Backend skickar bara en regel för uppgifter som har inlämningssätt, och Approved kräver betygsskala.
  return Boolean(part.completionRule) && part.gradeScale !== null && part.gradeScale !== 'None'
}

const {run: save, loading: saving, error} = useAsync(async () => {
  if (cycle.value) {
    return
  }
  const {data} = await api.PUT('/api/courses/{id}/modules/{moduleId}/roadmap', {
    params: {path: {id: props.courseId, moduleId: props.module.id}},
    body: {
      links: props.module.content.map((part) => ({
        linkId: part.linkId,
        blockedByLinkIds: blockedBy.value[part.linkId] ?? [],
        completionRule: rules.value[part.linkId] ?? null,
      })),
    },
  })
  if (data) {
    emit('saved', data)
  }
  open.value = false
})

watch(open, (isOpen) => {
  if (isOpen) {
    reset()
  }
}, {immediate: true})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="max-h-[88vh] overflow-y-auto sm:max-w-3xl">
      <form class="flex flex-col gap-4" @submit.prevent="save">
        <DialogHeader>
          <DialogTitle>Roadmap för {{ module.title }}</DialogTitle>
          <DialogDescription>
            Delarna är grupperade i steg. En del som ligger i ett senare steg öppnas när delarna
            den är låst av är klara. Utkast i veckan publiceras när du sparar.
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="cycleText" variant="destructive" data-test="roadmap-cycle">
          <AlertDescription>
            Låsningarna går runt: {{ cycleText }}. Ta bort en av dem för att kunna spara.
          </AlertDescription>
        </Alert>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <div class="bg-muted/40 flex flex-wrap items-center justify-between gap-3 rounded-lg border px-3 py-2">
          <div class="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
            <span><strong class="text-foreground">{{ stages.length }}</strong> steg</span>
            <span><strong class="text-foreground">{{ directCount }}</strong> tillgängliga direkt</span>
            <span><strong class="text-foreground">{{ lockedCount }}</strong> kräver en annan del</span>
          </div>
          <label class="flex w-fit items-center gap-2 text-sm">
            <Checkbox v-model="preview" data-test="roadmap-preview"/>
            Förhandsvisa som ny elev
          </label>
        </div>

        <div class="flex flex-col">
          <template v-for="(stage, stageIndex) in stageRows" :key="stage.index">
            <div
              class="flex flex-wrap items-center gap-2 px-1 pb-2"
              :data-test="`roadmap-stage-${stageIndex}`"
            >
              <Badge variant="secondary" class="font-mono text-[11px]">
                {{ stageLabel(stageIndex) }}
              </Badge>
              <span class="text-sm font-medium">{{ stageHint(stageIndex) }}</span>
              <span class="text-muted-foreground ml-auto text-xs">
                {{ countLabel(stage.rows.length, 'del', 'delar') }}
              </span>
            </div>

            <div class="divide-y overflow-hidden rounded-xl border">
              <div
                v-for="row in stage.rows"
                :key="row.node.linkId"
                class="px-4 py-3"
                :data-test="`roadmap-part-${row.node.linkId}`"
              >
                <div class="flex items-start gap-3">
                  <component :is="partOf(row.part).icon" class="text-muted-foreground mt-0.5 size-4 shrink-0"/>
                  <div class="min-w-0 flex-1">
                    <div class="flex flex-wrap items-center gap-2">
                      <span class="text-sm font-medium">{{ row.node.title }}</span>
                      <span class="text-muted-foreground text-xs">{{ partOf(row.part).label }}</span>
                      <StatusBadge v-if="row.part.status === 'Draft'" status="publishing.draft"/>
                    </div>

                    <template v-if="row.part.type !== 'News'">
                      <div class="mt-2 flex flex-col gap-1.5 text-xs">
                        <div v-if="row.blockers.length > 0" class="flex flex-wrap items-center gap-1.5">
                          <span class="text-muted-foreground">Låst av</span>
                          <Badge
                            v-for="blocker in row.blockers"
                            :key="blocker.linkId"
                            variant="secondary"
                            class="gap-1 pr-1"
                            data-test="roadmap-blocker"
                          >
                            {{ blocker.title }}
                            <button
                              v-if="!preview"
                              type="button"
                              :aria-label="`Ta bort låsningen ${blocker.title}`"
                              @click="removeBlocker(row.node, blocker.linkId)"
                            >
                              <PhX class="size-3"/>
                            </button>
                          </Badge>
                        </div>
                        <span v-else class="text-muted-foreground flex items-center gap-1">
                          <PhLockOpen class="size-3.5"/>
                          Tillgänglig direkt
                        </span>

                        <div
                          class="flex flex-wrap items-center gap-1.5"
                          data-test="roadmap-unlocks"
                        >
                          <span class="text-muted-foreground flex items-center gap-1">
                            <PhLockOpen class="size-3.5"/>
                            {{ row.unlocks.length > 0
                              ? `Låser upp ${countLabel(row.unlocks.length, 'del', 'delar')}`
                              : 'Låser inte upp något' }}
                          </span>
                          <Badge
                            v-for="unlocked in row.unlocks"
                            :key="unlocked.linkId"
                            variant="outline"
                          >
                            {{ unlocked.title }}
                          </Badge>
                        </div>
                      </div>

                      <div v-if="!preview" class="mt-3 flex flex-wrap items-center gap-2">
                        <NativeSelect
                          size="sm"
                          :aria-label="`Lägg till låsning för ${row.node.title}`"
                          :model-value="picked[row.node.linkId] ?? ''"
                          @update:model-value="addBlocker(row.node, $event)"
                        >
                          <NativeSelectOption value="">Lägg till låsning…</NativeSelectOption>
                          <template
                            v-for="(candidateStage, candidateIndex) in stages"
                            :key="candidateIndex"
                          >
                            <optgroup
                              v-if="candidatesIn(candidateStage, row.node).length > 0"
                              :label="stageLabel(candidateIndex)"
                            >
                              <NativeSelectOption
                                v-for="candidate in candidatesIn(candidateStage, row.node)"
                                :key="candidate.linkId"
                                :value="candidate.linkId"
                              >
                                {{ candidate.title }}
                              </NativeSelectOption>
                            </optgroup>
                          </template>
                        </NativeSelect>

                        <template v-if="isSubmission(row.part)">
                          <span class="text-muted-foreground text-xs">Räknas som klar</span>
                          <NativeSelect
                            size="sm"
                            :aria-label="`När ${row.node.title} räknas som klar`"
                            :model-value="rules[row.node.linkId] ?? 'Submitted'"
                            @update:model-value="setRule(row.node.linkId, $event)"
                          >
                            <NativeSelectOption value="Submitted">Inlämnad räcker</NativeSelectOption>
                            <NativeSelectOption value="Approved">Godkänd krävs</NativeSelectOption>
                          </NativeSelect>
                        </template>
                      </div>

                      <p
                        v-if="row.part.status === 'Draft'"
                        class="text-muted-foreground mt-2 flex items-center gap-1.5 text-xs"
                        data-test="roadmap-draft"
                      >
                        <PhEye class="size-3.5"/>
                        Utkast – publiceras automatiskt när du sparar roadmapen.
                      </p>
                    </template>
                    <p v-else class="text-muted-foreground mt-1 text-xs">
                      Nyheter kan inte ingå i roadmapen.
                    </p>

                    <p v-if="warningOf(row)" class="text-destructive mt-2 text-xs" data-test="roadmap-warning">
                      {{ warningOf(row) }}
                    </p>

                    <p v-if="preview" class="mt-2 flex items-center gap-1.5 text-xs" data-test="roadmap-preview-state">
                      <template v-if="row.preview === 'available'">
                        <PhLockOpen class="size-3.5 text-success"/>
                        Tillgänglig för en ny elev
                      </template>
                      <template v-else-if="row.preview === 'locked'">
                        <PhLock class="size-3.5"/>
                        Låst för en ny elev
                      </template>
                      <template v-else>
                        <PhEyeSlash class="size-3.5"/>
                        Syns inte än
                      </template>
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <div
              v-if="stageIndex < stageRows.length - 1"
              class="text-muted-foreground flex items-center justify-center gap-2 py-2 text-xs"
            >
              <PhArrowDown class="size-3.5"/>
              När delarna ovan är klara
            </div>
          </template>
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            :disabled="saving"
            @click="open = false"
          >
            Avbryt
          </Button>
          <Button type="submit" :disabled="saving || cycleText !== ''" data-test="save-roadmap">
            Spara roadmap
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
