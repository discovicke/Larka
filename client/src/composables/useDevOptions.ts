import {reactive} from 'vue'

import type {Option} from '@/components/dev/types'
import {api, type Schema} from '@/lib/api'

export type OptionSource
  = 'schools' | 'users' | 'groups' | 'courses' | 'modules' | 'contents' | 'contentLinks' | 'submissions'

type FeedItem = Schema<'ContentFeedItem'>

const sources: OptionSource[] = [
  'schools',
  'users',
  'groups',
  'courses',
  'modules',
  'contents',
  'contentLinks',
  'submissions',
]

function perSource<T>(value: () => T): Record<OptionSource, T> {
  return Object.fromEntries(sources.map((source) => [source, value()])) as Record<OptionSource, T>
}

// Delas av alla formulär på /dev, så en ny grupp syns i alla grupp-dropdowns efter en uppdatering.
const options = reactive(perSource<Option[]>(() => []))
const loading = reactive(perSource(() => false))

// Det som skapats på sidan. Ny content syns inte i något flöde förrän den publicerats,
// så utan det här går den inte att välja i "Publish content".
const created = perSource<Option[]>(() => [])

function unique(items: Option[]): Option[] {
  return [...new Map(items.map((option) => [option.value, option])).values()]
}

// Svar som inte är 2xx kastas som ApiError, så data finns alltid för en lista.
async function loadGroups(): Promise<Schema<'GroupDto'>[]> {
  return (await api.GET('/api/groups')).data ?? []
}

async function loadCourses(): Promise<Schema<'CourseDto'>[]> {
  return (await api.GET('/api/courses')).data ?? []
}

async function loadModules(course: Schema<'CourseDto'>): Promise<Option[]> {
  const {data} = await api.GET('/api/courses/{id}/modules', {params: {path: {id: course.id}}})
  return (data ?? []).map((module) => ({value: module.id, label: `${course.code}: ${module.title}`}))
}

async function loadGroupFeed(id: string): Promise<FeedItem[]> {
  return (await api.GET('/api/groups/{id}/content', {params: {path: {id}}})).data ?? []
}

async function loadCourseFeed(id: string): Promise<FeedItem[]> {
  return (await api.GET('/api/courses/{id}/content', {params: {path: {id}}})).data ?? []
}

// Det finns inget GET för all content, så den samlas ihop från alla grupp- och kursflöden.
async function loadFeed(): Promise<{item: FeedItem, target: string}[]> {
  const [groups, courses] = await Promise.all([loadGroups(), loadCourses()])
  const feeds = await Promise.all([
    ...groups.map(async (group) => (await loadGroupFeed(group.id)).map((item) => ({item, target: group.name}))),
    ...courses.map(async (course) => (await loadCourseFeed(course.id)).map((item) => ({item, target: course.code}))),
  ])
  return feeds.flat()
}

async function loadAssignmentLinks(): Promise<Option[]> {
  const feed = await loadFeed()
  return feed
    .filter(({item}) => item.type === 'Assignment')
    .map(({item, target}) => ({value: item.linkId, label: `${item.title} → ${target}`}))
}

async function loadSubmissions(link: Option): Promise<Option[]> {
  const {data} = await api.GET('/api/links/{id}/submissions', {params: {path: {id: link.value}}})
  return (data ?? []).map((submission) => ({value: submission.id, label: `${link.label}: ${submission.status}`}))
}

const loaders: Record<OptionSource, () => Promise<Option[]>> = {
  schools: async () => ((await api.GET('/api/schools')).data ?? []).map((school) => ({
    value: school.id,
    label: school.name,
  })),
  users: async () => ((await api.GET('/api/users')).data ?? []).map((user) => ({
    value: user.id,
    label: `${user.name} (${user.role})`,
  })),
  groups: async () => (await loadGroups()).map((group) => ({value: group.id, label: `${group.name} (${group.term})`})),
  courses: async () => (await loadCourses()).map((course) => ({value: course.id, label: `${course.code} ${course.title}`})),
  modules: async () => (await Promise.all((await loadCourses()).map(loadModules))).flat(),
  contents: async () => (await loadFeed()).map(({item}) => ({
    value: item.contentId,
    label: `${item.title} (${item.type})`,
  })),
  contentLinks: loadAssignmentLinks,
  submissions: async () => {
    const links = unique([...(await loadAssignmentLinks()), ...created.contentLinks])
    return (await Promise.all(links.map(loadSubmissions))).flat()
  },
}

export function useDevOptions() {
  async function refresh(source: OptionSource) {
    const load = loaders[source]
    // Flera dropdowns kan dela källa, så den hämtas bara en gång åt gången.
    if (loading[source]) {
      return
    }

    loading[source] = true
    try {
      options[source] = unique([...(await load()), ...created[source]])
    } catch {
      // Dropdownen behåller det den hade. Felet syns när man skickar formuläret.
    } finally {
      loading[source] = false
    }
  }

  /** Lägger till något som just skapats, så att det går att välja direkt. */
  function remember(source: OptionSource, option: Option) {
    created[source].push(option)
    options[source] = unique([...options[source], option])
  }

  function labelOf(source: OptionSource, value: string): string | undefined {
    return options[source].find((option) => option.value === value)?.label
  }

  return {options, loading, refresh, remember, labelOf}
}
