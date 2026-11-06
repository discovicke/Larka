<script setup lang="ts">
import {
  PhArrowCounterClockwise,
  PhArrowLeft,
  PhArrowRight,
  PhCalendarBlank,
  PhCircle,
  PhLock,
  PhLockOpen,
} from '@phosphor-icons/vue'
import {computed, nextTick, ref, watch} from 'vue'
import {useRoute} from 'vue-router'

import GitHubAssignmentBlock from '@/components/GitHubAssignmentBlock.vue'
import RelatedMaterial from '@/components/student/RelatedMaterial.vue'
import StudentHelpButton from '@/components/StudentHelpButton.vue'
import AttachmentList from '@/components/course/AttachmentList.vue'
import PartFacts, {type PartFact} from '@/components/course/PartFacts.vue'
import MarkdownView from '@/components/editor/MarkdownView.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import SubmissionPanel from '@/components/student/SubmissionPanel.vue'
import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Checkbox} from '@/components/ui/checkbox'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, errorMessage, type Schema} from '@/lib/api'
import {parseChecklist} from '@/lib/checklist'
import {formatDate, partOf} from '@/lib/content'
import {
  dueHeadings,
  dueStatus,
  isDone,
  kindOf,
  nextDueAt,
  nextDueKind,
  rowIcon,
  rowIconClass,
  sameInstant,
  statusHeading,
} from '@/lib/studentCourse'
import {
  gradeBadgeVariant,
  gradedLabel,
  gradeLabel,
  gradeName,
  gradeScaleName,
  gradeScaleShort,
  handInLabel,
} from '@/lib/submissions'
import {fetchLinkSubmissions} from '@/lib/submissionsApi'
import {relativeText} from '@/lib/today'

// En öppen del: innehåll, checklista, lärarens kommentar och elevens status. Vyn runtom äger
// datat, valet av del och felmeddelandet.

const props = defineProps<{
  courseId: string
  part: Schema<'StudentContentItem'>
  module: Schema<'StudentModuleWithContentDto'>
  savingLinkId: string
  previousPart?: Schema<'StudentContentItem'> | null
  nextPart?: Schema<'StudentContentItem'> | null
}>()

const emit = defineEmits<{
  close: []
  open: [item: Schema<'StudentContentItem'>]
  complete: [linkId: string, completed: boolean]
  submitted: []
  error: [message: string]
}>()

const route = useRoute()
const root = ref<HTMLElement | null>(null)
const {me} = useCurrentUser()

const unlocked = computed(() => props.module.content.filter((item) =>
  item.blockedBy.some((blocker) => blocker.linkId === props.part.linkId)))
const canAskForHelp = computed(() => props.part.type === 'Assignment'
  && !props.part.isLocked
  && props.part.submissionStatus !== 'Submitted'
  && props.part.submissionStatus !== 'Graded')

// Checklistan i uppgiftstexten (`- [ ] ...`) visas som egna bockrutor. Elevens bockar sparas på servern.
const checklist = computed(() => !props.part.isLocked
  ? parseChecklist(props.part.bodyMd ?? '')
  : {items: [], body: ''})
const checkedIndexes = ref<number[]>([])

watch(() => checklist.value.items.length > 0
  ? props.part.linkId
  : '', async (linkId) => {
  checkedIndexes.value = []
  if (!linkId) {
    return
  }
  try {
    const {data} = await api.GET('/api/courses/{courseId}/links/{linkId}/checklist', {
      params: {path: {courseId: props.courseId, linkId}},
    })
    if (props.part.linkId === linkId) {
      checkedIndexes.value = data?.checkedIndexes ?? []
    }
  } catch (exception) {
    emit('error', errorMessage(exception))
  }
}, {immediate: true})

// Bockar direkt och ångrar om servern säger nej.
async function toggleChecklist(index: number, checked: boolean) {
  const linkId = props.part.linkId
  const before = checkedIndexes.value
  checkedIndexes.value = checked
    ? [...before.filter((candidate) => candidate !== index), index]
    : before.filter((candidate) => candidate !== index)
  try {
    await api.PUT('/api/courses/{courseId}/links/{linkId}/checklist/{index}', {
      params: {path: {courseId: props.courseId, linkId, index: String(index)}},
      body: {checked},
    })
  } catch (exception) {
    if (props.part.linkId === linkId) {
      checkedIndexes.value = before
    }
    emit('error', errorMessage(exception))
  }
}

const feedbackHeading = computed(() => {
  if (props.part.submissionStatus === 'NeedsWork') {
    return 'Läraren vill att du kompletterar'
  }
  return 'Lärarens kommentar'
})

// Lärarens kommentar och betyg hör till inlämningen, så de hämtas när en bedömd uppgift öppnas.
const feedback = ref<Schema<'GradeDto'> | null>(null)

watch(() => {
  const item = props.part
  return item.type === 'Assignment' && (item.submissionStatus === 'Graded' || item.submissionStatus === 'NeedsWork')
    ? item.linkId
    : ''
}, async (linkId) => {
  feedback.value = null
  if (!linkId) {
    return
  }
  try {
    const submissions = await fetchLinkSubmissions(linkId)
    if (props.part.linkId === linkId) {
      feedback.value = submissions[0]?.grade ?? null
    }
  } catch {
    // Kommentaren är ett tillägg; statusen i sidopanelen visas ändå.
  }
}, {immediate: true})

// Byter man del med Föregående och Nästa del ska den nya delen börja högst upp.
watch(() => props.part.linkId, async (_linkId, previous) => {
  if (previous) {
    await nextTick()
    root.value?.scrollIntoView?.({block: 'start'})
  }
})

function repoUrlFor(item: Schema<'StudentContentItem'>): string | null {
  return item.repo?.repoFullName
    ? `https://github.com/${item.repo.repoFullName}`
    : null
}

// Deadlinen under titeln är den som gäller nu: huvuddeadlinen, elevens egen eller en komplettering.
const due = computed(() => {
  const item = props.part
  const date = nextDueAt(item)
  if (!date) {
    return null
  }
  return {
    label: dueHeadings[nextDueKind(item)],
    date: formatDate(date, true),
    // Klar eller inlämnad behöver ingen nedräkning. Snart och försenad visas som märke i stället.
    relative: isDone(item)
      ? ''
      : relativeText(date, new Date()),
  }
})

// Övriga fakta står i statuspanelen. Datumet som redan står under titeln upprepas inte.
const facts = computed<PartFact[]>(() => {
  const item = props.part
  const kind = nextDueKind(item)
  const current = item.deadline?.nextDeadline ?? null
  const resubmits = item.resubmitDueAt
    .filter((value) => kind !== 'resubmit' || current === null || !sameInstant(value, current))
  return [
    {key: 'handin', label: 'Lämnas in', value: capitalize(handInOf(item))},
    {key: 'scale', label: 'Bedöms', value: gradeScaleShort(item.gradeScale), title: gradeScaleName(item.gradeScale)},
    {key: 'main-due', label: 'Ordinarie deadline', value: kind !== 'main' && item.dueAt
      ? formatDate(item.dueAt, true)
      : ''},
    {key: 'personal-due', label: 'Din deadline', value: kind !== 'personal' && item.personalDueAt
      ? formatDate(item.personalDueAt, true)
      : ''},
    {key: 'resubmit', label: resubmits.length === 1
      ? 'Komplettering'
      : 'Kompletteringar', value: resubmits.map((value) => formatDate(value, true)).join(', ')},
  ]
})

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1)
}

function handInOf(item: Schema<'StudentContentItem'>): string {
  return item.type === 'Assignment'
    ? handInLabel(item.submissionModes)
    : ''
}
</script>

<template>
  <div ref="root" class="flex scroll-mt-4 flex-col gap-6" data-test="student-detail">
    <header class="flex flex-col gap-2 border-b pb-4">
      <p class="text-muted-foreground flex flex-wrap items-center gap-x-1.5 gap-y-1 text-sm" data-test="part-eyebrow">
        <component :is="partOf(part).icon" class="size-4"/>
        <span>{{ kindOf(part) }}</span>
        <span aria-hidden="true">·</span>
        <!-- Veckan leder tillbaka till veckans lista, så sidan behöver ingen egen tillbakaknapp. -->
        <button
          type="button"
          class="hover:text-foreground decoration-border hover:decoration-foreground min-h-6 text-left underline underline-offset-4"
          :aria-label="`Tillbaka till ${module.title}`"
          data-test="student-back"
          @click="emit('close')"
        >
          {{ module.title }}
        </button>
        <template v-if="part.type === 'News'">
          <span aria-hidden="true">·</span>
          <span>Nyhet</span>
        </template>
      </p>
      <h2 class="text-3xl leading-tight font-semibold">{{ part.title }}</h2>
      <p v-if="due" class="flex flex-wrap items-center gap-x-2 gap-y-1 text-[15px]" data-test="fact-due">
        <PhCalendarBlank class="text-muted-foreground size-4"/>
        <span>{{ due.label }} <strong class="font-semibold">{{ due.date }}</strong></span>
        <StatusBadge v-if="dueStatus(part)" :status="dueStatus(part)!"/>
        <span v-else-if="due.relative" class="text-muted-foreground">{{ due.relative }}</span>
      </p>
    </header>

    <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_360px]">
      <div class="flex flex-col gap-6">
        <section v-if="part.isLocked" class="bg-card flex flex-col items-start gap-3 rounded-xl border border-dashed p-6">
          <span class="bg-muted grid size-10 place-items-center rounded-full">
            <PhLock class="text-muted-foreground size-5"/>
          </span>
          <div>
            <h2 class="text-lg font-semibold">Den här delen är låst</h2>
            <p class="text-muted-foreground text-sm">
              Öppnas när du har klarat de delar den väntar på. Börja med delarna i Nästa steg.
            </p>
          </div>
        </section>

        <template v-else>
          <section class="flex max-w-[68ch] flex-col gap-3">
            <h2 class="text-lg font-semibold">
              {{ part.type === 'Assignment'
                ? 'Uppgiften'
                : 'Innehåll' }}
            </h2>
            <MarkdownView
              v-if="checklist.body"
              :markdown="checklist.body"
              class="text-foreground/90 text-sm"
            />
            <p v-else-if="checklist.items.length === 0" class="text-muted-foreground text-sm">
              Inget innehåll än.
            </p>
          </section>

          <section
            v-if="checklist.items.length > 0"
            class="flex max-w-[68ch] flex-col gap-3"
            aria-labelledby="checklist-h"
            data-test="student-checklist"
          >
            <h2 id="checklist-h" class="text-lg font-semibold">Att lämna in</h2>
            <ul class="flex flex-col gap-2">
              <li v-for="entry in checklist.items" :key="entry.index">
                <label class="flex items-start gap-2.5 text-sm">
                  <Checkbox
                    class="mt-0.5"
                    :model-value="checkedIndexes.includes(entry.index)"
                    :data-test="`checklist-${entry.index}`"
                    @update:model-value="(value) => toggleChecklist(entry.index, value === true)"
                  />
                  <span>{{ entry.text }}</span>
                </label>
              </li>
            </ul>
          </section>

          <AttachmentList :content-id="part.contentId" :can-manage="false" class="max-w-[68ch]"/>
        </template>

        <RelatedMaterial :links="part.relatedLinks ?? []"/>

        <GitHubAssignmentBlock
          v-if="part.hasRepoMode && !part.isLocked"
          :git-hub-login="me?.gitHubLogin"
          :repo-url="repoUrlFor(part)"
          :group-name="part.group?.name"
          :group-members="part.group?.members.map((member) => member.name)"
          :latest-commit="part.repo?.lastSha"
          :check-status="part.repo?.checkStatus"
          :locked-at="part.repo?.lockedAt"
          :submission-sha="part.repo?.submissionSha"
          :lock-error="part.repo?.lockError"
          :repo-provisioning-state="part.repo?.provisioningState"
          :repo-provisioning-error="part.repo?.provisioningError"
          :requirements="part.requirements"
          :check-runs="part.repo?.checkRuns"
          :redirect-to="route.fullPath"
        />

        <!-- Inlämningen står under uppgiften i huvudspalten, så att eleven skriver med uppgiften ovanför och har plats. -->
        <SubmissionPanel
          v-if="part.type === 'Assignment' && !part.isLocked"
          :item="part"
          @submitted="emit('submitted')"
        />

        <section
          v-if="part.type !== 'News' && !part.isLocked"
          class="bg-card overflow-hidden rounded-xl border"
          aria-labelledby="unlocks-h"
        >
          <div class="bg-muted/40 border-b px-4 py-2.5">
            <h2 id="unlocks-h" class="text-sm font-semibold">Låser upp</h2>
          </div>
          <div class="flex flex-col gap-2 px-4 py-3 text-sm">
            <p v-if="unlocked.length > 0">När du är klar med den här delen öppnas:</p>
            <p v-else class="text-muted-foreground">
              Den här delen låser inte upp någon annan del.
            </p>
            <ul v-if="unlocked.length > 0" class="flex flex-col gap-2">
              <li
                v-for="item in unlocked"
                :key="item.linkId"
                class="flex items-center gap-2"
              >
                <PhLockOpen class="size-4 text-success"/>
                <span class="font-medium">{{ item.title }}</span>
                <span class="text-muted-foreground text-xs">{{ kindOf(item) }}</span>
              </li>
            </ul>
          </div>
        </section>

        <nav
          v-if="previousPart || nextPart"
          class="flex flex-wrap items-center justify-between gap-2 border-t pt-4"
          aria-label="Föregående och nästa del"
          data-test="student-part-nav"
        >
          <Button
            v-if="previousPart"
            variant="ghost"
            class="max-w-full"
            data-test="student-previous"
            @click="emit('open', previousPart)"
          >
            <PhArrowLeft/>
            <span class="truncate">Föregående: {{ previousPart.title }}</span>
          </Button>
          <span v-else/>
          <Button
            v-if="nextPart"
            variant="outline"
            class="max-w-full"
            data-test="student-next"
            @click="emit('open', nextPart)"
          >
            <span class="truncate">Nästa del: {{ nextPart.title }}</span>
            <PhArrowRight/>
          </Button>
        </nav>
      </div>

      <aside class="flex flex-col gap-4 max-lg:order-first">
        <section class="bg-card flex flex-col gap-3 rounded-xl border p-4" aria-labelledby="status-h">
          <h2 id="status-h" class="text-sm font-semibold">Din status</h2>
          <div class="flex items-center gap-2">
            <component :is="rowIcon(part)" class="size-5" :class="rowIconClass(part)"/>
            <span class="font-medium">{{ statusHeading(part) }}</span>
          </div>

          <!-- Kompletteringens återkoppling står i inlämningen, ovanför formuläret. Här visas bara läget. -->
          <section
            v-if="feedback && part.submissionStatus !== 'NeedsWork'"
            class="flex flex-col gap-2 border-t pt-3"
            aria-labelledby="feedback-h"
            data-test="student-feedback"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <h2 id="feedback-h" class="text-sm font-semibold">
                {{ feedbackHeading }}
              </h2>
              <Badge
                v-if="feedback.level && part.submissionStatus === 'Graded'"
                :variant="gradeBadgeVariant(feedback.level)"
                role="img"
                :aria-label="gradeName(feedback.level)"
                :title="gradeName(feedback.level)"
                data-test="grade-stamp"
              >
                {{ gradeLabel(feedback.level) }}
              </Badge>
            </div>
            <p v-if="feedback.feedback" class="text-sm leading-relaxed whitespace-pre-wrap wrap-anywhere">
              {{ feedback.feedback }}
            </p>
            <p class="text-muted-foreground text-xs" data-test="graded-by">{{ gradedLabel(feedback, true) }}</p>
          </section>

          <PartFacts :facts="facts" class="border-t pt-3"/>

          <template v-if="part.canMarkDone && !part.isLocked">
            <Button
              v-if="!part.isCompleted"
              :disabled="savingLinkId === part.linkId"
              :data-test="`complete-${part.linkId}`"
              @click="emit('complete', part.linkId, true)"
            >
              <PhCircle/>
              Klarmarkera
            </Button>
            <Button
              v-else
              variant="outline"
              :disabled="savingLinkId === part.linkId"
              :data-test="`undo-${part.linkId}`"
              @click="emit('complete', part.linkId, false)"
            >
              <PhArrowCounterClockwise/>
              Ångra
            </Button>
          </template>
        </section>

        <StudentHelpButton
          v-if="canAskForHelp"
          :link-id="part.linkId"
        />
      </aside>
    </div>
  </div>
</template>
