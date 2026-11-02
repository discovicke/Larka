import type {Schema} from '@/lib/api'
import type {PartType} from '@/lib/content'
import {isoOrNull} from '@/lib/form'

type SubmissionMode = Schema<'SubmissionMode'>

export interface RequirementForm {
  title: string
  testPattern: string
  isManual: boolean
}

export interface GroupForm {
  id?: string
  name: string
  studentIds: string[]
}

export interface ContentForm {
  partType: PartType
  title: string
  bodyMd: string
  publishAt: string // yyyy-mm-ddThh:mm (lokal tid), tom = nu
  dueAt: string
  resubmitDueAt: string[] // kompletteringsdeadlines, yyyy-mm-ddThh:mm (lokal tid)
  gradeScale: Schema<'GradeScale'>
  submissionModes: SubmissionMode[]
  templateRepo: string
  repoNamePattern: string
  workMode: Schema<'RepoWorkMode'>
  groups: GroupForm[]
  relatedLinkIds: string[]
  requirements: RequirementForm[]
  isPinned: boolean
}

export type FormErrors = Partial<Record<
  'title' | 'dueAt' | 'resubmitDueAt' | 'templateRepo' | 'repoNamePattern' | 'groups' | 'requirements',
  string
>>

/** Så många kompletteringsdeadlines tillåter backend. */
export const maxResubmitDueAt = 5

export const defaultRepoNamePattern = '{assignmentSlug}-{githubLogin}'
export const defaultGroupRepoNamePattern = '{assignmentSlug}-{groupSlug}'

export function emptyForm(): ContentForm {
  return {
    partType: 'Theory',
    title: '',
    bodyMd: '',
    publishAt: '',
    dueAt: '',
    resubmitDueAt: [],
    gradeScale: 'PassFail',
    submissionModes: [],
    templateRepo: '',
    repoNamePattern: defaultRepoNamePattern,
    workMode: 'Individual',
    groups: [],
    relatedLinkIds: [],
    requirements: [],
    isPinned: false,
  }
}

export const isAssignment = (form: ContentForm) => form.partType !== 'Theory'

export const isGraded = (form: ContentForm) => form.partType === 'Submission'

export const usesRepo = (form: ContentForm) => isAssignment(form) && form.submissionModes.includes('Repo')

function resubmitErrors(form: ContentForm): string | undefined {
  if (form.resubmitDueAt.length === 0) {
    return undefined
  }
  const filled = form.resubmitDueAt.filter((value) => value !== '')
  if (!form.dueAt) {
    return 'Ange sista dagen först.'
  }
  if (form.resubmitDueAt.some((value) => value === '')) {
    return 'Fyll i datum och tid, eller ta bort raden.'
  }
  const due = new Date(form.dueAt)
  if (filled.some((value) => new Date(value) <= due)) {
    return 'Kompletteringsdeadlines måste vara efter sista dagen.'
  }
  if (new Set(filled.map((value) => new Date(value).getTime())).size !== filled.length) {
    return 'Kompletteringsdeadlines får inte vara samma tid två gånger.'
  }
  return undefined
}

export const usesGroupRepo = (form: ContentForm) => usesRepo(form) && form.workMode === 'Group'

export function validate(
  form: ContentForm,
  requireGroups = true,
  groupStudentIds: string[] = [],
): FormErrors {
  const errors: FormErrors = {}
  if (!form.title.trim()) {
    errors.title = 'Ange en titel.'
  }
  const publishAt = form.publishAt
    ? new Date(form.publishAt)
    : new Date()
  if (isAssignment(form) && form.dueAt && new Date(form.dueAt) <= publishAt) {
    errors.dueAt = 'Sista dagen måste vara efter att delen publiceras.'
  }
  if (isAssignment(form)) {
    const resubmit = resubmitErrors(form)
    if (resubmit) {
      errors.resubmitDueAt = resubmit
    }
  }
  if (usesRepo(form) && !form.templateRepo.trim()) {
    errors.templateRepo = 'Ange mallrepot, t.ex. edugrade-net25/mall-lab2.'
  }
  if (usesRepo(form)) {
    const pattern = form.repoNamePattern.trim()
    const groupPlaceholders = usesGroupRepo(form)
      ? /\{(groupName|groupSlug|groupNumber)\}/i
      : /\{(githubLogin|studentEmail|studentId)\}/i
    if (pattern && !groupPlaceholders.test(pattern)) {
      errors.repoNamePattern = usesGroupRepo(form)
        ? 'Mönstret måste innehålla {groupName}, {groupSlug} eller {groupNumber}.'
        : 'Mönstret måste innehålla {githubLogin}, {studentEmail} eller {studentId}.'
    }
    if (pattern && /[/\\]/.test(pattern)) {
      errors.repoNamePattern = 'Mönstret får inte innehålla slash.'
    }
    if (usesGroupRepo(form) && requireGroups) {
      if (form.groups.length === 0) {
        errors.groups = 'Slumpa eller lägg till minst en grupp.'
      } else if (form.groups.some((group) => !group.name.trim())) {
        errors.groups = 'Alla grupper måste ha ett namn.'
      } else if (form.groups.some((group) => group.studentIds.length === 0)) {
        errors.groups = 'Alla grupper måste innehålla minst en elev.'
      } else if (new Set(form.groups.map((group) => group.name.trim().toLowerCase())).size !== form.groups.length) {
        errors.groups = 'Gruppnamnen måste vara unika.'
      } else if (groupStudentIds.length > 0) {
        const assigned = new Set(form.groups.flatMap((group) => group.studentIds))
        if (groupStudentIds.some((studentId) => !assigned.has(studentId))) {
          errors.groups = 'Alla elever i kursen måste tillhöra en grupp.'
        }
      }
    }
    if (form.requirements.some((requirement) =>
      !requirement.title.trim()
      || (!requirement.isManual && !requirement.testPattern.trim()))) {
      errors.requirements = 'Varje krav måste ha en text och ett testmönster, eller markeras som manuellt.'
    }
  }
  return errors
}

/** Innehållet och länken skickas tillsammans till POST /api/content/with-link. */
export function toRequests(
  form: ContentForm,
  status: Schema<'ContentStatus'>,
  target: {courseId: string, moduleId: string},
): {content: Schema<'CreateContentDto'>, link: Schema<'CreateContentLinkDto'>} {
  const link = {...target, publishAt: isoOrNull(form.publishAt), isPinned: form.isPinned}
  if (!isAssignment(form)) {
    return {content: {type: 'Theory', title: form.title.trim(), bodyMd: form.bodyMd, status}, link}
  }

  return {
    content: {
      type: 'Assignment',
      title: form.title.trim(),
      bodyMd: form.bodyMd,
      status,
      gradeScale: isGraded(form)
        ? form.gradeScale
        : null,
      submissionModes: form.submissionModes,
      templateRepo: usesRepo(form)
        ? form.templateRepo.trim()
        : null,
      repoNamePattern: usesRepo(form)
        ? form.repoNamePattern.trim() || null
        : null,
      workMode: usesRepo(form)
        ? form.workMode
        : 'Individual',
      lockAtDeadline: true,
      feedbackPullRequest: false,
      allowLate: false,
      requirements: usesRepo(form)
        ? form.requirements.map((requirement) => ({
            title: requirement.title.trim(),
            testPattern: requirement.isManual
              ? null
              : requirement.testPattern.trim(),
            isManual: requirement.isManual,
          }))
        : [],
    },
    link: {
      ...link,
      dueAt: isoOrNull(form.dueAt),
      groups: usesGroupRepo(form)
        ? form.groups.map((group) => ({
            name: group.name.trim(),
            studentIds: group.studentIds,
          }))
        : null,
      relatedLinkIds: form.relatedLinkIds,
      resubmitDueAt: resubmitIso(form),
    },
  }
}

function resubmitIso(form: ContentForm): string[] {
  if (!form.dueAt) {
    return []
  }
  return form.resubmitDueAt.flatMap((value) => {
    const iso = isoOrNull(value)
    return iso
      ? [iso]
      : []
  })
}

/** Ändringarna skickas till innehållet och länken. Typ och status ligger utanför. */
export function toUpdateRequests(
  form: ContentForm,
): {content: Schema<'UpdateContentDto'>, link: Schema<'UpdateContentLinkDto'>} {
  const link = {
    publishAt: isoOrNull(form.publishAt),
    dueAt: isAssignment(form)
      ? isoOrNull(form.dueAt)
      : null,
    // Bara uppgifter har kompletteringsdeadlines. Utelämnade behålls av API:et.
    ...(isAssignment(form)
      ? {resubmitDueAt: resubmitIso(form)}
      : {}),
    isPinned: form.isPinned,
    relatedLinkIds: form.relatedLinkIds,
  }
  if (!isAssignment(form)) {
    return {
      content: {title: form.title.trim(), bodyMd: form.bodyMd, gradeScale: null, submissionModes: null, templateRepo: null},
      link,
    }
  }

  return {
    content: {
      title: form.title.trim(),
      bodyMd: form.bodyMd,
      gradeScale: isGraded(form)
        ? form.gradeScale
        : null,
      submissionModes: form.submissionModes,
      templateRepo: usesRepo(form)
        ? form.templateRepo.trim()
        : null,
      repoNamePattern: usesRepo(form)
        ? form.repoNamePattern.trim() || null
        : null,
      workMode: usesRepo(form)
        ? form.workMode
        : 'Individual',
      lockAtDeadline: true,
      feedbackPullRequest: false,
      allowLate: false,
      requirements: usesRepo(form)
        ? form.requirements.map((requirement) => ({
            title: requirement.title.trim(),
            testPattern: requirement.isManual
              ? null
              : requirement.testPattern.trim(),
            isManual: requirement.isManual,
          }))
        : [],
    },
    link,
  }
}

/** Stegen i "När du publicerar", efter vad läraren valt. */
export function publishSteps(form: ContentForm): string[] {
  const when = form.publishAt
    ? 'på publiceringsdagen'
    : 'direkt'
  if (!isAssignment(form)) {
    return [`Delen syns för kursens elever ${when}.`]
  }

  const steps = [`Uppgiften dyker upp under Att göra för kursens elever ${when}.`]
  if (usesGroupRepo(form)) {
    steps.push('Eleverna delas in i grupper och varje grupp får ett gemensamt privat repo.')
  } else if (usesRepo(form)) {
    steps.push('Varje elev får ett eget privat repo från mallen. Du och eleven får åtkomst.')
  }
  if (form.submissionModes.includes('OnSite')) {
    steps.push('Du klarmarkerar eleverna på lektionen.')
  }
  if (form.submissionModes.length > 0) {
    steps.push(form.dueAt
      ? 'Vid deadline hamnar inlämningarna under Att bedöma.'
      : 'Inlämningarna hamnar under Att bedöma.')
  }
  return steps
}
