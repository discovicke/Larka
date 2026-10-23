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
  return Object.fromEntries(sources.map((s) => [s, value()])) as Record<OptionSource, T>
}

// Delas av alla formulär på /dev, så en ny grupp syns i alla grupp-dropdowns efter en uppdatering.
const options = reactive(perSource<Option[]>(() => []))
const loading = reactive(perSource(() => false))

// Det som skapats på sidan. Ny content syns inte i något flöde förrän den publicerats,
// så utan det här går den inte att välja i "Publish content".
const created = perSource<Option[]>(() => [])

function unique(items: Option[]): Option[] {
  return [...new Map(items.map((o) => [o.value, o])).values()]
}

async function loadGroups(): Promise<Group[]> {
  return api.get<Group[]>('/api/groups')
}

// Det finns inget GET /api/courses än (#32), så kurserna samlas ihop från alla grupper.
// Kurser som ingen grupp läser syns därför inte.
async function loadCourses(): Promise<Course[]> {
  const groups = await loadGroups()
  const perGroup = await Promise.all(groups.map((g) => api.get<Course[]>(`/api/groups/${g.id}/courses`)))
  return [...new Map(perGroup.flat().map((c) => [c.id, c])).values()]
}

// Det finns inget GET för all content, så den samlas ihop från alla grupp- och kursflöden.
async function loadFeed(): Promise<{item: FeedItem, target: string}[]> {
  const [groups, courses] = await Promise.all([loadGroups(), loadCourses()])
  const feeds = await Promise.all([
    ...groups.map(async (g) => (await api.get<FeedItem[]>(`/api/groups/${g.id}/content`))
      .map((item) => ({item, target: g.name}))),
    ...courses.map(async (c) => (await api.get<FeedItem[]>(`/api/courses/${c.id}/content`))
      .map((item) => ({item, target: c.code}))),
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
  groups: async () => (await loadGroups()).map((g) => ({value: g.id, label: `${g.name} (${g.term})`})),
  courses: async () => (await loadCourses()).map((c) => ({value: c.id, label: `${c.code} ${c.title}`})),
  contents: async () => (await loadFeed()).map(({item}) => ({
    value: item.contentId,
    label: `${item.title} (${item.type})`,
  })),
  contentLinks: loadAssignmentLinks,
  submissions: async () => {
    const links = unique([...(await loadAssignmentLinks()), ...created.contentLinks])
    const perLink = await Promise.all(links.map(async (link) =>
      (await api.get<Submission[]>(`/api/links/${link.value}/submissions`))
        .map((s) => ({value: s.id, label: `${link.label}: ${s.status}`}))))
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
    return options[source].find((o) => o.value === value)?.label
  }

  return {options, loading, refresh, remember, labelOf}
}
