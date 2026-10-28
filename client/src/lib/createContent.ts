import type {Schema} from '@/lib/api'
import type {PartType} from '@/lib/content'
import {isoOrNull} from '@/lib/form'

type SubmissionMode = Schema<'SubmissionMode'>

export interface ContentForm {
  partType: PartType
  title: string
  bodyMd: string
  publishAt: string // yyyy-mm-ddThh:mm (lokal tid), tom = nu
  dueAt: string
  gradeScale: Schema<'GradeScale'>
  submissionModes: SubmissionMode[]
  templateRepo: string
  isPinned: boolean
}

export type FormErrors = Partial<Record<'title' | 'dueAt' | 'templateRepo', string>>

export function emptyForm(): ContentForm {
  return {
    partType: 'Theory',
    title: '',
    bodyMd: '',
    publishAt: '',
    dueAt: '',
    gradeScale: 'PassFail',
    submissionModes: [],
    templateRepo: '',
    isPinned: false,
  }
}

export const isAssignment = (form: ContentForm) => form.partType !== 'Theory'

export const isGraded = (form: ContentForm) => form.partType === 'Submission'

export const usesRepo = (form: ContentForm) => isAssignment(form) && form.submissionModes.includes('Repo')

export function validate(form: ContentForm): FormErrors {
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
  if (usesRepo(form) && !form.templateRepo.trim()) {
    errors.templateRepo = 'Ange mallrepot, t.ex. edugrade-net25/mall-lab2.'
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
    },
    link: {...link, dueAt: isoOrNull(form.dueAt)},
  }
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
    isPinned: form.isPinned,
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
  if (usesRepo(form)) {
    steps.push('Varje elev får ett eget privat repo från mallen. Du och eleven får åtkomst.')
  }
  if (form.submissionModes.includes('OnSite')) {
    steps.push('Du bockar av eleverna på lektionen.')
  }
  if (form.submissionModes.length > 0) {
    steps.push(form.dueAt
      ? 'Vid deadline hamnar inlämningarna under Att bedöma.'
      : 'Inlämningarna hamnar under Att bedöma.')
  }
  return steps
}
