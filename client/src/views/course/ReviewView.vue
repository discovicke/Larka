<script setup lang="ts">
import {PhFile, PhInfo} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'
import {
  useRoute,
  useRouter,
} from 'vue-router'

import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Alert, AlertDescription} from '@/components/ui/alert'
import {Avatar, AvatarFallback} from '@/components/ui/avatar'
import {Button} from '@/components/ui/button'
import {Skeleton} from '@/components/ui/skeleton'
import {Textarea} from '@/components/ui/textarea'
import {useCrumbLabels} from '@/composables/useCrumbLabels'
import {useCurrentUser} from '@/composables/useCurrentUser'
import {api, type Schema} from '@/lib/api'
import {formatDate} from '@/lib/content'
import {initials} from '@/lib/news'
import {checkClass, checkLabel, formatSize, gradeLabel, gradeName, levelsFor} from '@/lib/submissions'

const route = useRoute()
const {setCrumbLabel} = useCrumbLabels()
const router = useRouter()
const courseId = computed(() => String(route.params.courseId))
const linkId = computed(() => String(route.params.linkId))
const submissionId = computed(() => String(route.params.submissionId))
// Mentorn läser inlämningen och betyget men bedömer inte.
const {can} = useCurrentUser()
const canGrade = computed(() => can('grade', courseId.value))

const submission = ref<Schema<'SubmissionDto'> | null>(null)
const assignment = ref<Schema<'ModuleContentItem'> | null>(null)
const loading = ref(true)
const loadError = ref('')
const feedback = ref('')
const level = ref<Schema<'GradeLevel'> | null>(null)
const formError = ref('')
const saveError = ref('')
const saving = ref(false)

const levels = computed(() => levelsFor(assignment.value?.gradeScale ?? null))
const hasGrades = computed(() => levels.value.length > 0)
const studentName = computed(() => submission.value?.studentName ?? '')
const repoUrl = computed(() => submission.value?.repo?.repoFullName
  ? `https://github.com/${submission.value.repo.repoFullName}`
  : '')
const shortSha = computed(() => submission.value?.repo?.lastSha?.slice(0, 7) ?? '–')
const gradedAt = computed(() => submission.value?.grade
  ? formatDate(submission.value.grade.gradedAt, true)
  : '')
const hasHandedIn = computed(() => Boolean(submission.value?.bodyMd)
  || (submission.value?.files.length ?? 0) > 0
  || Boolean(submission.value?.repo))

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [submissionResult, moduleResult] = await Promise.all([
      api.GET('/api/submissions/{id}', {params: {path: {id: submissionId.value}}}),
      api.GET('/api/courses/{id}/modules', {params: {path: {id: courseId.value}}}),
    ])
    const found = submissionResult.data ?? null
    submission.value = found
    const week = (moduleResult.data ?? [])
      .find((candidate) => candidate.content.some((item) => item.linkId === linkId.value))
    assignment.value = week?.content.find((item) => item.linkId === linkId.value) ?? null
    setCrumbLabel(`assignment:${linkId.value}`, assignment.value?.title)
    setCrumbLabel(`student:${submissionId.value}`, studentName.value)
    if (found?.grade) {
      level.value = found.grade.level
      feedback.value = found.grade.feedback
    } else {
      level.value = null
      feedback.value = ''
    }
  } catch {
    loadError.value = 'Kunde inte hämta inlämningen.'
  } finally {
    loading.value = false
  }
}

async function save(body: Schema<'GradeSubmissionDto'>) {
  const current = submission.value
  if (!current) {
    return
  }
  saving.value = true
  saveError.value = ''
  try {
    await api.POST('/api/submissions/{id}/grade', {params: {path: {id: current.id}}, body})
    await router.push({name: 'assignment-submissions', params: {courseId: courseId.value, linkId: linkId.value}})
  } catch {
    saveError.value = 'Kunde inte spara bedömningen.'
  } finally {
    saving.value = false
  }
}

async function publish() {
  formError.value = ''
  if (hasGrades.value && level.value === null) {
    formError.value = 'Välj ett betyg.'
    return
  }
  await save({feedback: feedback.value, level: level.value, needsWork: false})
}

async function requestWork() {
  formError.value = ''
  if (!feedback.value.trim()) {
    formError.value = 'Skriv vad eleven ska komplettera.'
    return
  }
  await save({feedback: feedback.value, level: null, needsWork: true})
}

watch([courseId, linkId, submissionId], () => {
  load()
}, {immediate: true})
</script>

<template>
  <div class="flex min-w-0 flex-1 flex-wrap items-stretch">
    <div v-if="loading" class="flex flex-1 flex-col gap-4 p-6">
      <Skeleton class="h-9 w-64"/>
      <Skeleton class="h-40 w-full max-w-[760px]"/>
    </div>

    <Alert v-else-if="loadError" variant="destructive" class="m-4">
      <AlertDescription>{{ loadError }}</AlertDescription>
    </Alert>

    <template v-else-if="submission">
      <div class="flex-[999_1_600px] min-w-0 flex flex-col">
        <div class="flex flex-wrap items-center gap-x-4 gap-y-2 border-b px-6 py-4">
          <Avatar class="size-9">
            <AvatarFallback class="text-xs font-semibold">{{ initials(studentName) }}</AvatarFallback>
          </Avatar>
          <div class="flex flex-[1_1_260px] flex-col">
            <h1 class="font-heading text-xl leading-tight font-semibold">{{ studentName }}</h1>
            <div class="text-muted-foreground text-[13px]">
              <span>{{ assignment?.title }}</span>
              <template v-if="submission.submittedAt">
                <span> · Inlämnad {{ formatDate(submission.submittedAt, true) }}</span>
              </template>
              <template v-if="submission.repo?.repoFullName">
                <span> · </span>
                <a
                  :href="repoUrl"
                  target="_blank"
                  rel="noopener"
                  class="font-mono text-xs underline underline-offset-3"
                >{{ submission.repo.repoFullName }}</a>
              </template>
            </div>
          </div>
        </div>

        <div class="flex max-w-[760px] flex-col gap-6 p-6">
          <p v-if="!hasHandedIn" class="text-muted-foreground text-sm">
            Eleven har inte lämnat in något än.
          </p>

          <template v-else>
            <section class="flex flex-col gap-2">
              <h2 class="text-sm font-semibold">Svar</h2>
              <MarkdownView v-if="submission.bodyMd" :markdown="submission.bodyMd"/>
              <p v-else class="text-muted-foreground text-sm">Inget textsvar.</p>
            </section>

            <section v-if="submission.files.length > 0" class="flex flex-col gap-2">
              <h2 class="text-sm font-semibold">Filer</h2>
              <a
                v-for="file in submission.files"
                :key="file.id"
                :href="file.url"
                target="_blank"
                rel="noopener"
                class="flex max-w-[420px] items-center gap-2.5 rounded-[7px] border px-3 py-2.5"
              >
                <PhFile class="text-muted-foreground size-4 shrink-0"/>
                <span class="font-medium">{{ file.fileName }}</span>
                <span class="text-muted-foreground text-xs">{{ formatSize(file.sizeBytes) }}</span>
              </a>
            </section>

            <section v-if="submission.repo" class="flex flex-col gap-2" data-test="review-repo">
              <h2 class="text-sm font-semibold">Repo</h2>
              <a
                v-if="repoUrl"
                :href="repoUrl"
                target="_blank"
                rel="noopener"
                class="font-mono text-[13px] underline underline-offset-3"
              >{{ submission.repo.repoFullName }}</a>
              <p v-else class="text-muted-foreground text-sm">
                {{ submission.repo.provisioningState === 'Failed'
                  ? 'Repot kunde inte skapas.'
                  : 'Repot skapas fortfarande.' }}
              </p>
              <p v-if="submission.repo.lastCommitMessage" class="text-sm">
                {{ submission.repo.lastCommitMessage }}
              </p>
              <p class="text-muted-foreground text-xs">
                Senaste commit <span class="font-mono">{{ shortSha }}</span>
                <template v-if="submission.repo.lastCommitAt">
                  · {{ formatDate(submission.repo.lastCommitAt, true) }}
                </template>
                · {{ submission.repo.commitCount }} commits
              </p>
              <p class="text-[13px]" :class="checkClass(submission.repo.checkStatus)">
                Tester: {{ submission.repo.checksPassed }}/{{ submission.repo.checksTotal }}
                · {{ checkLabel(submission.repo.checkStatus) }}
              </p>
              <p v-if="submission.repo.submissionSha" class="text-muted-foreground text-xs">
                Inlämnad commit <span class="font-mono">{{ submission.repo.submissionSha.slice(0, 7) }}</span>
              </p>
              <p v-if="submission.repo.reopenedAt" class="text-warning text-xs">
                Öppnat för komplettering {{ formatDate(submission.repo.reopenedAt, true) }}.
                Eleven kan committa igen.
              </p>
              <p v-if="submission.repo.lockError" class="text-warning text-xs">
                {{ submission.repo.lockError }}
              </p>
            </section>
          </template>
        </div>
      </div>

      <aside
        aria-label="Bedömning"
        class="flex-[1_1_340px] min-w-0 border-l bg-muted/40 p-5 flex flex-col gap-5 max-md:border-l-0 max-md:border-t"
      >
        <section class="flex flex-col gap-2">
          <h2 class="text-sm font-semibold">Betyg</h2>
          <div
            v-if="hasGrades && canGrade"
            role="radiogroup"
            class="grid gap-2"
            :class="levels.length === 3
              ? 'grid-cols-3'
              : 'grid-cols-2'"
          >
            <button
              v-for="option in levels"
              :key="option"
              type="button"
              role="radio"
              :aria-checked="level === option"
              class="h-11 rounded-[7px] border bg-background flex flex-col items-center justify-center leading-tight"
              :class="level === option
                ? 'bg-primary text-primary-foreground border-primary'
                : ''"
              @click="level = option"
            >
              <span class="font-bold">{{ gradeLabel(option) }}</span>
              <span
                class="text-[11px] text-muted-foreground"
                :class="level === option
                  ? 'text-primary-foreground/80'
                  : ''"
              >{{ gradeName(option) }}</span>
            </button>
          </div>
          <p v-else-if="hasGrades" class="text-sm" data-test="read-only-grade">
            {{ level
              ? `${gradeLabel(level)} · ${gradeName(level)}`
              : 'Inte bedömd än.' }}
          </p>
          <p v-else class="text-muted-foreground text-sm">Uppgiften har inget betyg, bara återkoppling.</p>
        </section>

        <div v-if="canGrade" class="flex flex-col gap-2">
          <label for="feedback" class="font-heading text-sm font-semibold">Sammanfattande kommentar</label>
          <Textarea
            id="feedback"
            v-model="feedback"
            :rows="4"
            class="bg-background"
            placeholder="Skriv en kommentar till eleven…"
          />
        </div>

        <section v-else-if="feedback" class="flex flex-col gap-2">
          <h2 class="text-sm font-semibold">Kommentar</h2>
          <p class="text-sm whitespace-pre-line">{{ feedback }}</p>
        </section>

        <p v-if="formError" class="text-destructive text-[13px]">{{ formError }}</p>

        <Alert v-if="saveError" variant="destructive">
          <AlertDescription>{{ saveError }}</AlertDescription>
        </Alert>

        <div class="mt-auto flex flex-col gap-2">
          <p v-if="gradedAt" class="text-muted-foreground text-xs">Bedömd {{ gradedAt }}</p>
          <template v-if="canGrade">
            <p class="text-muted-foreground flex items-center gap-1.5 text-xs">
              <PhInfo class="size-3.5"/>
              Eleven ser betyget och kommentaren direkt.
            </p>
            <Button class="w-full" :disabled="saving" @click="publish">Publicera bedömning</Button>
            <Button
              variant="outline"
              class="w-full"
              :disabled="saving"
              @click="requestWork"
            >
              Begär komplettering
            </Button>
          </template>
        </div>
      </aside>
    </template>
  </div>
</template>
