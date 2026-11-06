<script setup lang="ts">
import {PhCalendarBlank, PhWarning} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Alert, AlertDescription, AlertTitle} from '@/components/ui/alert'
import {Button} from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import {FieldLabel} from '@/components/ui/field'
import {RadioGroup, RadioGroupItem} from '@/components/ui/radio-group'
import {useAsync} from '@/composables/useAsync'
import {api, type Schema} from '@/lib/api'
import {countLabel, formatDate} from '@/lib/content'

const props = defineProps<{
  courseId: string
  module: Schema<'ModuleWithContentDto'>
  item: Schema<'ModuleContentItem'>
  modules: Schema<'ModuleWithContentDto'>[]
}>()

const open = defineModel<boolean>('open', {required: true})
const emit = defineEmits<{moved: [result: Schema<'MoveContentLinkResultDto'>]}>()

const targetId = ref('')

const targets = computed(() => props.modules.filter((module) => module.id !== props.module.id))
const hasActivity = computed(() => props.item.hasStudentActivity)
const isLive = computed(() =>
  props.item.status === 'Published' && new Date(props.item.publishAt) <= new Date())
const isScheduled = computed(() =>
  props.item.status === 'Published' && new Date(props.item.publishAt) > new Date())
const blockedByTitles = computed(() => (props.item.blockedBy ?? [])
  .map((linkId) => props.module.content.find((part) => part.linkId === linkId)?.title ?? linkId))
const unlocksTitles = computed(() => props.module.content
  .filter((part) => (part.blockedBy ?? []).includes(props.item.linkId))
  .map((part) => part.title))
const hasDependencies = computed(() =>
  blockedByTitles.value.length > 0 || unlocksTitles.value.length > 0)

watch(open, (isOpen) => {
  if (isOpen) {
    targetId.value = ''
    error.value = ''
  }
})

const {run: move, loading: saving, error} = useAsync(async () => {
  if (!targetId.value || hasActivity.value) {
    return
  }

  const {data} = await api.PUT('/api/content/{id}/links/{linkId}/module', {
    params: {path: {id: props.item.contentId, linkId: props.item.linkId}},
    body: {moduleId: targetId.value},
  })
  if (data) {
    emit('moved', data)
  }
  open.value = false
})
</script>

<template>
  <Dialog v-model:open="open">
    <DialogContent class="sm:max-w-md">
      <form class="flex min-w-0 flex-col gap-4" @submit.prevent="move">
        <DialogHeader>
          <DialogTitle>Flytta {{ item.title }}</DialogTitle>
          <DialogDescription>
            Välj vilken vecka delen ska ligga i. Delen behåller sitt id och sin deadline, och
            hamnar sist i målveckan.
          </DialogDescription>
        </DialogHeader>

        <Alert v-if="hasActivity" variant="destructive" data-test="move-activity-warning">
          <AlertDescription>
            Eleven har redan arbetat med den här delen. Den kan därför inte flyttas.
          </AlertDescription>
        </Alert>

        <Alert v-if="isLive || isScheduled" data-test="move-published-warning">
          <AlertDescription>
            {{ isLive
              ? 'Delen är publicerad'
              : `Delen publiceras ${formatDate(item.publishAt, true)}` }}.
            Den fortsätter vara synlig för eleverna efter flytten, men byter plats i kursen.
          </AlertDescription>
        </Alert>

        <Alert v-if="hasDependencies" data-test="move-dependency-warning">
          <PhWarning/>
          <AlertTitle>Beroenden tas bort</AlertTitle>
          <AlertDescription class="flex flex-col gap-1.5">
            <span v-if="blockedByTitles.length > 0">
              Delen var låst av: {{ blockedByTitles.join(', ') }}.
            </span>
            <span v-if="unlocksTitles.length > 0">
              Delen låste upp: {{ unlocksTitles.join(', ') }}.
            </span>
            <span>Beroenden gäller bara inom en vecka och tas bort när delen flyttas.</span>
          </AlertDescription>
        </Alert>

        <Alert v-if="error" variant="destructive">
          <AlertDescription>{{ error }}</AlertDescription>
        </Alert>

        <RadioGroup
          v-if="targets.length > 0"
          v-model="targetId"
          aria-label="Välj målvecka"
          class="min-w-0"
        >
          <FieldLabel
            v-for="target in targets"
            :key="target.id"
            class="has-data-checked:border-primary/60 hover:bg-muted/50 w-full cursor-pointer items-center gap-3 rounded-lg border p-3 font-normal transition-colors"
            @click="targetId = target.id"
          >
            <RadioGroupItem
              :value="target.id"
              :data-test="`move-target-${target.id}`"
            />
            <PhCalendarBlank class="text-muted-foreground size-4 shrink-0"/>
            <span class="min-w-0 flex-1">
              <span class="block truncate text-sm font-medium">{{ target.title }}</span>
              <span class="text-muted-foreground text-xs">
                {{ countLabel(target.content.length, 'del', 'delar') }}
              </span>
            </span>
          </FieldLabel>
        </RadioGroup>
        <p v-else class="text-muted-foreground text-sm">
          Det finns ingen annan vecka att flytta delen till. Skapa en vecka först.
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
          <Button
            type="submit"
            :disabled="saving || hasActivity || !targetId"
            data-test="confirm-move"
          >
            Flytta
          </Button>
        </DialogFooter>
      </form>
    </DialogContent>
  </Dialog>
</template>
