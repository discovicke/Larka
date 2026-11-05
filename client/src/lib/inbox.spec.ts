import {describe, expect, it} from 'vitest'

import type {Schema} from '@/lib/api'
import {inboxKindOf, notificationTitle} from '@/lib/inbox'

function personalDeadline(personalDueAt: string | null): Schema<'NotificationDto'> {
  return {
    id: 'n1',
    kind: 'PersonalDeadlineSet',
    contentId: 'x1',
    submissionId: 's1',
    linkId: 'l1',
    title: 'Labb 1',
    courseId: 'c1',
    courseTitle: 'DevOps',
    groupName: null,
    level: null,
    createdAt: '2026-11-03T10:00:00Z',
    readAt: null,
    personalDueAt,
  }
}

describe('notis om personlig deadline', () => {
  it('blir en egen rad i inkorgen', () => {
    expect(inboxKindOf(personalDeadline(null))).toBe('personalDeadline')
  })

  it('nämner uppgiften och datumet', () => {
    const title = notificationTitle(personalDeadline('2026-11-20T22:59:00Z'), 'personalDeadline')

    expect(title).toContain('Ny deadline för Labb 1:')
    expect(title).toContain('nov')
  })

  it('klarar att datumet har tagits bort sedan dess', () => {
    expect(notificationTitle(personalDeadline(null), 'personalDeadline')).toBe('Ny deadline för Labb 1')
  })
})

describe('notiser om kommentarer', () => {
  function commentNotification(
    kind: 'CommentMention' | 'CommentReply',
    actorName: string | null,
  ): Schema<'NotificationDto'> {
    return {...personalDeadline(null), kind, title: 'Studiebesök', commentId: 'k1', actorName}
  }

  it('blir egna rader', () => {
    expect(inboxKindOf(commentNotification('CommentMention', 'Bo Elev'))).toBe('mention')
    expect(inboxKindOf(commentNotification('CommentReply', 'Bo Elev'))).toBe('reply')
  })

  it('säger vem som nämnde en och var', () => {
    expect(notificationTitle(commentNotification('CommentMention', 'Bo Elev'), 'mention'))
      .toBe('Bo Elev nämnde dig i en kommentar på Studiebesök')
    expect(notificationTitle(commentNotification('CommentMention', null), 'mention'))
      .toBe('Någon nämnde dig i en kommentar på Studiebesök')
    expect(notificationTitle(commentNotification('CommentReply', 'Bo Elev'), 'reply'))
      .toBe('Ny kommentar på Studiebesök')
  })
})
