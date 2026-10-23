import {reactive} from 'vue'

import type {Option} from '@/components/dev/types'
import {api} from '@/lib/api'

export type OptionSource
  = 'schools' | 'users' | 'groups' | 'courses' | 'contents' | 'contentLinks' | 'submissions'

interface Group {
  id: string
  name: string
  term: string
}

interface Course {
  id: string
  title: string
  code: string
}

interface FeedItem {
  linkId: string
  contentId: string
  type: string
  title: string
}

interface Submission {
  id: string
  status: string
}

const sources: OptionSource[] = [
  'schools',
  'users',
  'groups',
  'courses',
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

async function loadGroups(): Promise<Group[]> {
  return api.get<Group[]>('/api/groups')
}

// Det finns inget GET /api/courses än (#32), så kurserna samlas ihop från alla grupper.
// Kurser som ingen grupp läser syns därför inte.
async function loadCourses(): Promise<Course[]> {
  const groups = await loadGroups()
  const perGroup = await Promise.all(groups.map((group) => api.get<Course[]>(`/api/groups/${group.id}/courses`)))
  return [...new Map(perGroup.flat().map((course) => [course.id, course])).values()]
}

// Det finns inget GET för all content, så den samlas ihop från alla grupp- och kursflöden.
async function loadFeed(): Promise<{item: FeedItem, target: string}[]> {
  const [groups, courses] = await Promise.all([loadGroups(), loadCourses()])
  const feeds = await Promise.all([
    ...groups.map(async (group) => (await api.get<FeedItem[]>(`/api/groups/${group.id}/content`))
      .map((item) => ({item, target: group.name}))),
    ...courses.map(async (course) => (await api.get<FeedItem[]>(`/api/courses/${course.id}/content`))
      .map((item) => ({item, target: course.code}))),
  ])
  return feeds.flat()
}

async function loadAssignmentLinks(): Promise<Option[]> {
  const feed = await loadFeed()
  return feed
    .filter(({item}) => item.type === 'Assignment')
    .map(({item, target}) => ({value: item.linkId, label: `${item.title} → ${target}`}))
}

const loaders: Partial<Record<OptionSource, () => Promise<Option[]>>> = {
  groups: async () => (await loadGroups()).map((group) => ({value: group.id, label: `${group.name} (${group.term})`})),
  courses: async () => (await loadCourses()).map((course) => ({value: course.id, label: `${course.code} ${course.title}`})),
  contents: async () => (await loadFeed()).map(({item}) => ({
    value: item.contentId,
    label: `${item.title} (${item.type})`,
  })),
  contentLinks: loadAssignmentLinks,
  submissions: async () => {
    const links = unique([...(await loadAssignmentLinks()), ...created.contentLinks])
    const perLink = await Promise.all(links.map(async (link) =>
      (await api.get<Submission[]>(`/api/links/${link.value}/submissions`))
        .map((submission) => ({value: submission.id, label: `${link.label}: ${submission.status}`}))))
    return perLink.flat()
  },
  // schools och users: kräver GET-endpoints från #32.
}

export function useDevOptions() {
  async function refresh(source: OptionSource) {
    const load = loaders[source]
    // Flera dropdowns kan dela källa, så den hämtas bara en gång åt gången.
    if (!load || loading[source]) {
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
