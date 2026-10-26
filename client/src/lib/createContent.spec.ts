import {describe, expect, it} from 'vitest'

import {type ContentForm, emptyForm, publishSteps, toRequests, validate} from './createContent'

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
      dueAt: '2026-11-01T10:00',
    }), 'Published', target)

    expect(content).toMatchObject({
      gradeScale: 'PassFailDistinction',
      submissionModes: ['Text', 'Repo'],
      templateRepo: 'org/mall',
    })
    expect(link.dueAt).toBe(new Date('2026-11-01T10:00').toISOString())
  })

  it('mallrepot skickas inte utan GitHub', () => {
    const {content} = toRequests(form({partType: 'Submission', submissionModes: ['File'], templateRepo: 'org/mall'}), 'Published', target)

    expect(content.templateRepo).toBeNull()
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
