import type {Schema} from '@/lib/api'

// Roadmap = veckans delar och vilka delar som måste vara klara först.
// Hjälparna är rena funktioner, så att både dialogen och testerna kan använda dem.

/** En del sedd som nod i veckans beroenden. */
export interface RoadmapNode {
  linkId: string
  title: string
  type: Schema<'ContentType'>
  status: Schema<'ContentStatus'>
  publishAt: string
  blockedBy: string[]
}

/** Gör om en del från API:et till en nod. blockedBy saknas i äldre svar och blir då tom. */
export function nodeOf(part: Schema<'ModuleContentItem'>): RoadmapNode {
  return {
    linkId: part.linkId,
    title: part.title,
    type: part.type,
    status: part.status,
    publishAt: part.publishAt,
    blockedBy: part.blockedBy ?? [],
  }
}

/** Delarna som den här delen låser upp, det vill säga de som har den som förutsättning. */
export function unlocksOf(nodes: RoadmapNode[], linkId: string): RoadmapNode[] {
  return nodes.filter((node) => node.blockedBy.includes(linkId))
}

/**
 * Veckans delar grupperade i steg. Steg 0 är delar utan förutsättningar, nästa steg är delar
 * som bara kräver delar i tidigare steg, och så vidare. En cykel läggs i ett sista steg så att
 * läraren fortfarande kan se och rätta den.
 */
export function stagesOf(nodes: RoadmapNode[]): RoadmapNode[][] {
  const ids = new Set(nodes.map((node) => node.linkId))
  const level = new Map<string, number>()
  let pending = [...nodes]

  while (pending.length > 0) {
    const next: RoadmapNode[] = []
    for (const node of pending) {
      const blockers = node.blockedBy.filter((linkId) => ids.has(linkId))
      if (blockers.every((linkId) => level.has(linkId))) {
        level.set(
          node.linkId,
          blockers.length === 0
            ? 0
            : Math.max(...blockers.map((linkId) => level.get(linkId)!)) + 1,
        )
      } else {
        next.push(node)
      }
    }

    if (next.length === pending.length) {
      // Ingen nod blev klar: resten ingår i en cykel. Lägg dem i sista steget.
      const fallback = Math.max(-1, ...level.values()) + 1
      for (const node of next) {
        level.set(node.linkId, fallback)
      }
      break
    }
    pending = next
  }

  const groups = new Map<number, RoadmapNode[]>()
  for (const node of nodes) {
    const index = level.get(node.linkId) ?? 0
    groups.set(index, [...(groups.get(index) ?? []), node])
  }
  return [...groups.entries()]
    .sort(([first], [second]) => first - second)
    .map(([, stage]) => stage)
}

/** Rubrik för ett steg: "Start", "Steg 2", "Steg 3" … */
export function stageLabel(index: number): string {
  return index === 0
    ? 'Start'
    : `Steg ${index + 1}`
}

/** Förklaringen under stegrubriken. */
export function stageHint(index: number): string {
  return index === 0
    ? 'Tillgängliga direkt'
    : 'Kräver delar i tidigare steg'
}

/**
 * Hittar en cykel i beroendena. Returnerar länk-id:n där första och sista är samma del,
 * t.ex. [A, B, A], eller null när grafen är acyklisk.
 */
export function findCycle(nodes: RoadmapNode[]): string[] | null {
  const byId = new Map(nodes.map((node) => [node.linkId, node]))
  const state = new Map<string, 'visiting' | 'done'>()
  const path: string[] = []

  function visit(node: RoadmapNode): string[] | null {
    state.set(node.linkId, 'visiting')
    path.push(node.linkId)
    for (const blockerId of node.blockedBy) {
      const blocker = byId.get(blockerId)
      if (!blocker) {
        continue
      }
      const blockerState = state.get(blockerId)
      if (blockerState === 'visiting') {
        return [...path.slice(path.indexOf(blockerId)), blockerId]
      }
      if (blockerState === undefined) {
        const cycle = visit(blocker)
        if (cycle) {
          return cycle
        }
      }
    }
    state.set(node.linkId, 'done')
    path.pop()
    return null
  }

  for (const node of nodes) {
    if (state.get(node.linkId) === undefined) {
      const cycle = visit(node)
      if (cycle) {
        return cycle
      }
    }
  }
  return null
}

/** "Teori A → Övning B → Teori A", med titlar i stället för id:n. */
export function cycleLabel(cycle: string[], nodes: RoadmapNode[]): string {
  const byId = new Map(nodes.map((node) => [node.linkId, node]))
  return cycle
    .map((linkId) => byId.get(linkId)?.title ?? linkId)
    .join(' → ')
}

/** Publicerad och PublishAt har passerat. */
export function isPublished(node: RoadmapNode, now = new Date()): boolean {
  return node.status === 'Published' && new Date(node.publishAt) <= now
}

export type PreviewState = 'available' | 'locked' | 'hidden'

/**
 * Hur en ny elev ser veckan. En ny elev har inget klart, så bara publicerade delar
 * utan förutsättningar är tillgängliga.
 */
export function previewState(node: RoadmapNode, now = new Date()): PreviewState {
  if (!isPublished(node, now)) {
    return 'hidden'
  }
  return node.blockedBy.length === 0
    ? 'available'
    : 'locked'
}
