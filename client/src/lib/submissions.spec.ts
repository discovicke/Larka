import {describe, expect, it} from 'vitest'

import type {Schema} from './api'
import {
  autoFailLabel,
  checkClass,
  checkLabel,
  gradeBadgeVariant,
  gradeLabel,
  gradeName,
  levelsFor,
  nextInQueue,
  statusLabel,
} from './submissions'

describe('nextInQueue', () => {
  const entry = (id: string, studentName: string, status: Schema<'SubmissionStatus'> = 'Submitted') =>
    ({id, studentName, status})
  const list = [
    entry('c', 'Örjan'),
    entry('a', 'Anna'),
    entry('g', 'Greta', 'Graded'),
    entry('b', 'Bo'),
    entry('n', 'Nils', 'NeedsWork'),
  ]

  it('tar nästa inlämnade på namn och hoppar över bedömda och kompletteringar', () => {
    expect(nextInQueue(list, entry('a', 'Anna'))?.id).toBe('b')
    expect(nextInQueue(list, entry('b', 'Bo'))?.id).toBe('c')
  })

  it('börjar om från början efter den sista', () => {
    expect(nextInQueue(list, entry('c', 'Örjan'))?.id).toBe('a')
  })

  it('hittar rätt plats även när den öppna redan är bedömd', () => {
    expect(nextInQueue(list, entry('g', 'Greta', 'Graded'))?.id).toBe('c')
  })

  it('ger null när ingen väntar', () => {
    expect(nextInQueue([entry('a', 'Anna')], entry('a', 'Anna'))).toBeNull()
    expect(nextInQueue([], entry('a', 'Anna'))).toBeNull()
  })
})

describe('gradeLabel', () => {
  it('förkortar betygsstegen som i listan', () => {
    expect(gradeLabel('Fail')).toBe('IG')
    expect(gradeLabel('Pass')).toBe('G')
    expect(gradeLabel('Distinction')).toBe('VG')
  })
})

describe('gradeBadgeVariant', () => {
  it('väljer stämpelns utseende per betygssteg', () => {
    expect(gradeBadgeVariant('Fail')).toBe('gradeFail')
    expect(gradeBadgeVariant('Pass')).toBe('gradePass')
    expect(gradeBadgeVariant('Distinction')).toBe('gradeDistinction')
  })
})

describe('gradeName', () => {
  it('skriver ut betygsstegen', () => {
    expect(gradeName('Fail')).toBe('Icke godkänd')
    expect(gradeName('Pass')).toBe('Godkänd')
    expect(gradeName('Distinction')).toBe('Väl godkänd')
  })
})

describe('levelsFor', () => {
  it('ger stegen för IG-G', () => {
    expect(levelsFor('PassFail')).toEqual(['Fail', 'Pass'])
  })

  it('ger stegen för IG-VG', () => {
    expect(levelsFor('PassFailDistinction')).toEqual(['Fail', 'Pass', 'Distinction'])
  })

  it('ger inga steg när betyg saknas', () => {
    expect(levelsFor('None')).toEqual([])
    expect(levelsFor(null)).toEqual([])
  })
})

describe('statusLabel', () => {
  it('beskriver statusen på raden', () => {
    expect(statusLabel({status: 'NotStarted', submittedAt: null})).toBe('Inte inlämnad')
    expect(statusLabel({status: 'Submitted', submittedAt: '2026-10-23T21:59:00Z'})).toContain('Inlämnad ')
    expect(statusLabel({status: 'NeedsWork', submittedAt: '2026-10-23T21:59:00Z'})).toBe('Komplettering')
    expect(statusLabel({status: 'Graded', submittedAt: '2026-10-23T21:59:00Z'})).toBe('Bedömd')
    expect(statusLabel({status: 'NotStarted', submittedAt: null}, true)).toBe('IG · Ingen inlämning')
    expect(statusLabel({status: 'NeedsWork', submittedAt: null}, true)).toBe('IG · Kompletteringen lämnades inte in')
  })

  it('klarar en inlämnad rad utan tidpunkt', () => {
    expect(statusLabel({status: 'Submitted', submittedAt: null})).toBe('Inlämnad')
  })
})

describe('checkLabel', () => {
  it('översätter testernas läge', () => {
    expect(checkLabel('Green')).toBe('Gröna')
    expect(checkLabel('Red')).toBe('Röda')
    expect(checkLabel('Pending')).toBe('Väntar')
  })
})

describe('checkClass', () => {
  it('ger färgen för testernas läge', () => {
    expect(checkClass('Green')).toBe('text-success')
    expect(checkClass('Red')).toBe('text-destructive')
    expect(checkClass('Pending')).toBe('text-muted-foreground')
  })
})

describe('autoFailLabel', () => {
  it('förklarar vad som saknades', () => {
    expect(autoFailLabel('NotStarted')).toBe('IG · Ingen inlämning')
    expect(autoFailLabel('NeedsWork')).toBe('IG · Kompletteringen lämnades inte in')
  })
})
