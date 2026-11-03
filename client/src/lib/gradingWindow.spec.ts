import {describe, expect, it} from 'vitest'

import {allowsFail, allowsNeedsWork, autoFailLabel, deadlineRemains, handInWindow, isAutoFail, nextDeadline} from '@/lib/gradingWindow'

const now = new Date('2026-11-10T12:00:00Z')
const due = '2026-11-05T12:00:00Z'
const resubmit = ['2026-11-15T12:00:00Z', '2026-11-25T12:00:00Z']

describe('nextDeadline', () => {
  it('ger ordinarie deadline före den och kompletteringarna efter', () => {
    expect(nextDeadline({dueAt: '2026-11-12T12:00:00Z', resubmitDueAt: resubmit}, now)).toBe('2026-11-12T12:00:00Z')
    expect(nextDeadline({dueAt: due, resubmitDueAt: resubmit}, now)).toBe(resubmit[0])
    expect(nextDeadline({dueAt: due, resubmitDueAt: resubmit}, new Date('2026-11-20T00:00:00Z'))).toBe(resubmit[1])
  })

  it('ger null när allt har passerat eller dueAt saknas', () => {
    expect(nextDeadline({dueAt: due, resubmitDueAt: resubmit}, new Date('2026-12-01T00:00:00Z'))).toBeNull()
    expect(nextDeadline({dueAt: null, resubmitDueAt: []}, now)).toBeNull()
    expect(deadlineRemains({dueAt: null, resubmitDueAt: []}, now)).toBe(false)
  })
})

describe('handInWindow', () => {
  const item = {dueAt: due, resubmitDueAt: resubmit}

  it('låser en inlämnad efter ordinarie deadline', () => {
    expect(handInWindow(item, 'Submitted', now)).toEqual({open: false, until: null, closed: 'locked'})
    expect(handInWindow(item, 'Submitted', new Date('2026-11-01T00:00:00Z')).open).toBe(true)
  })

  it('öppnar övriga till nästa deadline som återstår', () => {
    for (const status of ['NotStarted', 'NeedsWork'] as const) {
      expect(handInWindow(item, status, now)).toEqual({open: true, until: resubmit[0], closed: null})
      expect(handInWindow(item, status, new Date('2026-12-01T00:00:00Z')).closed).toBe('closed')
    }
  })

  it('är stängd för bedömda och alltid öppen utan dueAt', () => {
    expect(handInWindow(item, 'Graded', now).open).toBe(false)
    expect(handInWindow({dueAt: null, resubmitDueAt: []}, 'NotStarted', now).open).toBe(true)
    expect(handInWindow({dueAt: null, resubmitDueAt: []}, 'Graded', now).open).toBe(false)
  })
})

describe('allowsFail och allowsNeedsWork', () => {
  it('IG först när ingen deadline återstår, Komplettera bara så länge en gör det', () => {
    const item = {dueAt: due, resubmitDueAt: resubmit}
    expect(allowsFail(item, now)).toBe(false)
    expect(allowsNeedsWork(item, now)).toBe(true)
    const after = new Date('2026-12-01T00:00:00Z')
    expect(allowsFail(item, after)).toBe(true)
    expect(allowsNeedsWork(item, after)).toBe(false)
  })

  it('tillåter allt utan dueAt', () => {
    const item = {dueAt: null, resubmitDueAt: []}
    expect(allowsFail(item, now)).toBe(true)
    expect(allowsNeedsWork(item, now)).toBe(true)
  })
})

describe('isAutoFail', () => {
  const item = {dueAt: due, resubmitDueAt: resubmit}
  const after = new Date('2026-12-01T00:00:00Z')

  it('är IG för uteblivet arbete när ingen deadline återstår', () => {
    expect(isAutoFail(item, 'NotStarted', after)).toBe(true)
    expect(isAutoFail(item, 'NeedsWork', after)).toBe(true)
  })

  it('är aldrig IG för inlämnat eller bedömt', () => {
    expect(isAutoFail(item, 'Submitted', after)).toBe(false)
    expect(isAutoFail(item, 'Graded', after)).toBe(false)
  })

  it('väntar så länge en deadline återstår', () => {
    expect(isAutoFail(item, 'NotStarted', now)).toBe(false)
    expect(isAutoFail(item, 'NeedsWork', now)).toBe(false)
    expect(isAutoFail({dueAt: due, resubmitDueAt: []}, 'NotStarted', now)).toBe(true)
  })

  it('gäller aldrig utan dueAt', () => {
    expect(isAutoFail({dueAt: null, resubmitDueAt: []}, 'NotStarted', after)).toBe(false)
  })

  it('förklarar vad som saknades', () => {
    expect(autoFailLabel('NotStarted')).toBe('IG · Ingen inlämning')
    expect(autoFailLabel('NeedsWork')).toBe('IG · Kompletteringen lämnades inte in')
  })
})

describe('personlig deadline', () => {
  const personal = '2026-12-10T12:00:00Z'
  const item = {dueAt: due, resubmitDueAt: resubmit, personalDueAt: personal}
  const after = new Date('2026-12-01T00:00:00Z')

  it('räknas som en deadline till och kan bli nästa', () => {
    expect(nextDeadline(item, after)).toBe(personal)
    expect(nextDeadline({...item, personalDueAt: '2026-11-12T12:00:00Z'}, now)).toBe('2026-11-12T12:00:00Z')
    expect(deadlineRemains(item, after)).toBe(true)
  })

  it('tar bort det automatiska IG:t tills den har passerat, och sedan kommer det tillbaka', () => {
    expect(isAutoFail({...item, personalDueAt: null}, 'NotStarted', after)).toBe(true)
    expect(isAutoFail(item, 'NotStarted', after)).toBe(false)
    expect(isAutoFail(item, 'NeedsWork', after)).toBe(false)
    expect(isAutoFail(item, 'NotStarted', new Date('2026-12-11T00:00:00Z'))).toBe(true)
  })

  it('öppnar fönstret för den som inte lämnat in, men låser en inlämnad efter dueAt', () => {
    expect(handInWindow(item, 'NotStarted', after)).toEqual({open: true, until: personal, closed: null})
    expect(handInWindow(item, 'Submitted', after)).toEqual({open: false, until: null, closed: 'locked'})
  })

  it('tillåter Komplettera men inte IG så länge den återstår, och ett tidigare datum ändrar inget', () => {
    expect(allowsNeedsWork(item, after)).toBe(true)
    expect(allowsFail(item, after)).toBe(false)
    expect(nextDeadline({...item, personalDueAt: '2026-11-06T12:00:00Z'}, now)).toBe(resubmit[0])
  })

  it('gäller inte utan dueAt', () => {
    expect(isAutoFail({dueAt: null, resubmitDueAt: [], personalDueAt: personal}, 'NotStarted', after)).toBe(false)
    expect(nextDeadline({dueAt: null, resubmitDueAt: [], personalDueAt: personal}, now)).toBeNull()
  })
})
