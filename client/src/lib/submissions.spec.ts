import {describe, expect, it} from 'vitest'

import {checkClass, checkLabel, formatSize, gradeLabel, gradeName, levelsFor, statusLabel} from './submissions'

describe('gradeLabel', () => {
  it('förkortar betygsstegen som i listan', () => {
    expect(gradeLabel('Fail')).toBe('IG')
    expect(gradeLabel('Pass')).toBe('G')
    expect(gradeLabel('Distinction')).toBe('VG')
  })
})

describe('gradeName', () => {
  it('skriver ut betygsstegen', () => {
    expect(gradeName('Fail')).toBe('Icke godkänt')
    expect(gradeName('Pass')).toBe('Godkänt')
    expect(gradeName('Distinction')).toBe('Väl godkänt')
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
    expect(statusLabel({status: 'NotStarted', submittedAt: null})).toBe('Ej inlämnad')
    expect(statusLabel({status: 'Submitted', submittedAt: '2026-10-23T21:59:00Z'})).toContain('Inlämnad ')
    expect(statusLabel({status: 'NeedsWork', submittedAt: '2026-10-23T21:59:00Z'})).toBe('Komplettering')
    expect(statusLabel({status: 'Graded', submittedAt: '2026-10-23T21:59:00Z'})).toBe('Bedömd')
  })

  it('klarar en inlämnad rad utan tidpunkt', () => {
    expect(statusLabel({status: 'Submitted', submittedAt: null})).toBe('Inlämnad')
  })
})

describe('formatSize', () => {
  it('visar byte utan decimal', () => {
    expect(formatSize(0)).toBe('0 B')
    expect(formatSize(512)).toBe('512 B')
  })

  it('visar kilobyte och megabyte med svenskt decimalkomma', () => {
    expect(formatSize(1500)).toBe('1,5 kB')
    expect(formatSize(12_500)).toBe('12,5 kB')
    expect(formatSize(2_345_678)).toBe('2,3 MB')
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
    expect(checkClass('Green')).toBe('text-emerald-700')
    expect(checkClass('Red')).toBe('text-red-700')
    expect(checkClass('Pending')).toBe('text-muted-foreground')
  })
})
