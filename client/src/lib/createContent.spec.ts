import {describe, expect, it} from 'vitest'

import {
  type ContentForm,
  emptyForm,
  publishSteps,
  toRequests,
  toUpdateRequests,
  validate,
} from './createContent'

const target = {courseId: 'c1', moduleId: 'm1'}

function form(changes: Partial<ContentForm>): ContentForm {
  return {...emptyForm(), title: 'Titel', ...changes}
}

describe('toRequests', () => {
  it('teori skickas utan uppgiftsfält och utan deadline', () => {
    const {content, link} = toRequests(form({bodyMd: '**Läs**', dueAt: '2026-11-01T10:00'}), 'Published', target)

    expect(content).toEqual({type: 'Theory', title: 'Titel', bodyMd: '**Läs**', status: 'Published'})
    expect(link).toEqual({courseId: 'c1', moduleId: 'm1', publishAt: null, isPinned: false})
  })

  it('en övning har ingen betygsskala', () => {
    const {content} = toRequests(form({partType: 'Exercise', gradeScale: 'PassFailDistinction'}), 'Draft', target)

    expect(content).toMatchObject({type: 'Assignment', gradeScale: null, status: 'Draft'})
  })

  it('en inlämning med GitHub skickar mallrepot, skala, sätt och deadline', () => {
    const {content, link} = toRequests(form({
      partType: 'Submission',
      gradeScale: 'PassFailDistinction',
      submissionModes: ['Text', 'Repo'],
      templateRepo: ' org/mall ',
      repoNamePattern: ' lab2-{githubLogin} ',
      dueAt: '2026-11-01T10:00',
    }), 'Published', target)

    expect(content).toMatchObject({
      gradeScale: 'PassFailDistinction',
      submissionModes: ['Text', 'Repo'],
      templateRepo: 'org/mall',
      repoNamePattern: 'lab2-{githubLogin}',
      workMode: 'Individual',
      lockAtDeadline: true,
      feedbackPullRequest: false,
      allowLate: false,
    })
    expect(link.dueAt).toBe(new Date('2026-11-01T10:00').toISOString())
  })

  it('mallrepot skickas inte utan GitHub', () => {
    const {content} = toRequests(form({partType: 'Submission', submissionModes: ['File'], templateRepo: 'org/mall'}), 'Published', target)

    expect(content.templateRepo).toBeNull()
  })
})

describe('toUpdateRequests', () => {
  it('teori uppdaterar bara innehållet och behåller pin', () => {
    const {content, link} = toUpdateRequests(form({bodyMd: '**Läs**', isPinned: true}))

    expect(content).toEqual({
      title: 'Titel',
      bodyMd: '**Läs**',
      gradeScale: null,
      submissionModes: null,
      templateRepo: null,
    })
    expect(link).toEqual({publishAt: null, dueAt: null, isPinned: true})
  })

  it('en inlämning uppdaterar skala, sätt, mallrepo och deadline', () => {
    const {content, link} = toUpdateRequests(form({
      partType: 'Submission',
      gradeScale: 'PassFailDistinction',
      submissionModes: ['Text', 'Repo'],
      templateRepo: ' org/mall ',
      repoNamePattern: 'lab2-{githubLogin}',
      dueAt: '2026-11-01T10:00',
    }))

    expect(content).toMatchObject({
      gradeScale: 'PassFailDistinction',
      submissionModes: ['Text', 'Repo'],
      templateRepo: 'org/mall',
      repoNamePattern: 'lab2-{githubLogin}',
    })
    expect(link.dueAt).toBe(new Date('2026-11-01T10:00').toISOString())
  })
})

describe('validate', () => {
  it('kräver titel', () => {
    expect(validate(form({title: ' '}))).toEqual({title: 'Ange en titel.'})
  })

  it('deadline måste vara efter publicering', () => {
    const errors = validate(form({partType: 'Submission', publishAt: '2026-11-02T08:00', dueAt: '2026-11-01T08:00'}))

    expect(Object.keys(errors)).toEqual(['dueAt'])
  })

  it('GitHub kräver mallrepo', () => {
    const errors = validate(form({partType: 'Submission', submissionModes: ['Repo']}))

    expect(Object.keys(errors)).toEqual(['templateRepo'])
  })

  it('GitHub-mönstret måste innehålla en unik platshållare', () => {
    const errors = validate(form({
      partType: 'Submission',
      submissionModes: ['Repo'],
      templateRepo: 'org/mall',
      repoNamePattern: 'lab2-{assignmentSlug}',
    }))

    expect(errors.repoNamePattern).toContain('{githubLogin}')
  })

  it('ett ifyllt formulär har inga fel', () => {
    expect(validate(form({partType: 'Submission', publishAt: '2026-11-01T08:00', dueAt: '2026-11-02T08:00'}))).toEqual({})
  })
})

describe('publishSteps', () => {
  it('följer valen', () => {
    expect(publishSteps(form({}))).toHaveLength(1)
    expect(publishSteps(form({partType: 'Submission', submissionModes: ['Repo'], dueAt: '2026-11-02T08:00'}))).toEqual([
      'Uppgiften dyker upp under Att göra för kursens elever direkt.',
      'Varje elev får ett eget privat repo från mallen. Du och eleven får åtkomst.',
      'Vid deadline hamnar inlämningarna under Att bedöma.',
    ])
  })
})
