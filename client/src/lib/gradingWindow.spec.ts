import {describe, expect, it} from 'vitest'

import {allowsFail, allowsNeedsWork, deadlineRemains, handInWindow, nextDeadline} from '@/lib/gradingWindow'

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
    for (const status of ['NotStarted', 'NeedsWork', 'NotSubmitted'] as const) {
      expect(handInWindow(item, status, now)).toEqual({open: true, until: resubmit[0], closed: null})
      expect(handInWindow(item, status, new Date('2026-12-01T00:00:00Z')).closed).toBe('closed')
    }
  })

  it('är stängd för bedömda och alltid öppen utan dueAt', () => {
    expect(handInWindow(item, 'Graded', now).open).toBe(false)
    expect(handInWindow({dueAt: null, resubmitDueAt: []}, 'NotSubmitted', now).open).toBe(true)
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
