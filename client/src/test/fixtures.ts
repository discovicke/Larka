import type {Schema} from '@/lib/api'

export function testCourse(overrides: Partial<Schema<'CourseDto'>> = {}): Schema<'CourseDto'> {
  return {
    id: 'c1',
    schoolId: 's1',
    title: 'DevOps',
    code: 'DEVOPS',
    description: '',
    points: null,
    startsOn: null,
    endsOn: null,
    ...overrides,
  }
}

export function testGroup(overrides: Partial<Schema<'GroupDto'>> = {}): Schema<'GroupDto'> {
  return {
    id: 'g1',
    schoolId: 's1',
    type: 'Class',
    name: 'NET25',
    memberCount: 0,
    lastCourseEndsOn: null,
    ...overrides,
  }
}

export function testNews(
  linkId: string,
  contentId: string,
  title: string,
  publishAt: string,
  extra: Partial<Schema<'ContentFeedItem'>> = {},
): Schema<'ContentFeedItem'> {
  return {
    linkId,
    contentId,
    type: 'News',
    title,
    bodyMd: `${title} – brödtext`,
    authorId: 'u1',
    authorName: 'Anna Lärare',
    groupId: null,
    courseId: null,
    moduleId: null,
    publishAt,
    dueAt: null,
    isPinned: false,
    ...extra,
  }
}

export function testNotification(
  id: string,
  kind: Schema<'NotificationKind'>,
  extra: Partial<Schema<'NotificationDto'>> = {},
): Schema<'NotificationDto'> {
  return {
    id,
    kind,
    contentId: null,
    submissionId: null,
    linkId: null,
    title: null,
    courseId: null,
    courseTitle: null,
    groupName: null,
    level: null,
    createdAt: '2026-10-19T10:00:00Z',
    readAt: null,
    ...extra,
  }
}
