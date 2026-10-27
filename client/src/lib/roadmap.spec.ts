import {describe, expect, it} from 'vitest'

import {
  cycleLabel,
  findCycle,
  isPublished,
  nodeOf,
  previewState,
  stageHint,
  stageLabel,
  stagesOf,
  unlockCount,
  unlocksOf,
  type RoadmapNode,
} from '@/lib/roadmap'

function node(linkId: string, blockedBy: string[] = [], overrides: Partial<RoadmapNode> = {}): RoadmapNode {
  return {
    linkId,
    title: `Del ${linkId}`,
    type: 'Theory',
    status: 'Published',
    publishAt: '2026-01-01T08:00:00Z',
    blockedBy,
    ...overrides,
  }
}

describe('roadmap', () => {
  it('gör om en del utan blockedBy till en nod med tom lista', () => {
    const part = {
      linkId: 'l1',
      contentId: 'c1',
      type: 'Theory' as const,
      gradeScale: null,
      completionRule: null,
      title: 'Teori',
      status: 'Published' as const,
      groupId: null,
      publishAt: '2026-01-01T08:00:00Z',
      dueAt: null,
      isPinned: false,
      blockedBy: [],
    }

    expect(nodeOf(part).blockedBy).toEqual([])
  })

  it('räknar hur många delar en del låser upp', () => {
    const nodes = [node('a'), node('b', ['a']), node('c', ['a', 'b'])]

    expect(unlockCount(nodes, 'a')).toBe(2)
    expect(unlockCount(nodes, 'b')).toBe(1)
    expect(unlockCount(nodes, 'c')).toBe(0)
  })

  it('ger delarna som en del låser upp', () => {
    const nodes = [node('a'), node('b', ['a']), node('c', ['a'])]

    expect(unlocksOf(nodes, 'a').map((item) => item.linkId)).toEqual(['b', 'c'])
    expect(unlocksOf(nodes, 'b')).toEqual([])
  })

  it('delar in veckan i steg efter vilka förutsättningar som krävs', () => {
    const nodes = [
      node('start'),
      node('andra', ['start']),
      node('också-start'),
      node('tredje', ['start', 'andra']),
    ]

    expect(stagesOf(nodes).map((stage) => stage.map((item) => item.linkId))).toEqual([
      ['start', 'också-start'],
      ['andra'],
      ['tredje'],
    ])
    expect(stageLabel(0)).toBe('Start')
    expect(stageLabel(1)).toBe('Steg 2')
    expect(stageHint(0)).toBe('Tillgängliga direkt')
    expect(stageHint(1)).toBe('Kräver delar i tidigare steg')
  })

  it('lägger en cykel i ett sista steg så att den går att rätta', () => {
    const stages = stagesOf([node('a', ['b']), node('b', ['a']), node('c')])

    expect(stages.map((stage) => stage.map((item) => item.linkId))).toEqual([
      ['c'],
      ['a', 'b'],
    ])
  })

  it('hittar en cykel och skriver ut den med titlar', () => {
    const nodes = [node('a', ['b']), node('b', ['a'])]

    const cycle = findCycle(nodes)

    expect(cycle).toEqual(['a', 'b', 'a'])
    expect(cycleLabel(cycle!, nodes)).toBe('Del a → Del b → Del a')
  })

  it('hittar ingen cykel i en kedja', () => {
    expect(findCycle([node('a'), node('b', ['a']), node('c', ['b'])])).toBeNull()
  })

  it('hoppar över förutsättningar som inte finns i veckan', () => {
    expect(findCycle([node('a', ['saknas'])])).toBeNull()
  })

  it('en del syns först när den är publicerad och PublishAt har passerat', () => {
    const now = new Date('2026-06-01T12:00:00Z')

    expect(isPublished(node('a'), now)).toBe(true)
    expect(isPublished(node('a', [], {status: 'Draft'}), now)).toBe(false)
    expect(isPublished(node('a', [], {publishAt: '2026-07-01T08:00:00Z'}), now)).toBe(false)
  })

  it('en ny elev ser bara publicerade delar utan förutsättningar som tillgängliga', () => {
    const now = new Date('2026-06-01T12:00:00Z')

    expect(previewState(node('a'), now)).toBe('available')
    expect(previewState(node('b', ['a']), now)).toBe('locked')
    expect(previewState(node('c', [], {status: 'Draft'}), now)).toBe('hidden')
  })
})
