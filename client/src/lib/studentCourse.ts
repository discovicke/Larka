import {PhCircle} from '@phosphor-icons/vue'
import type {Component} from 'vue'

import type {Schema} from '@/lib/api'
import {formatDate, partOf} from '@/lib/content'
import {deadlineStatusKey, partStatusKey, statuses, toneTextClasses} from '@/lib/status'

type Item = Schema<'StudentContentItem'>
type Module = Schema<'StudentModuleWithContentDto'>

export type StudentFilter = 'all' | 'todo' | 'submissions'

export interface PartSection {
  key: 'next' | 'unlocking' | 'later' | 'news' | 'completed'
  title: string
  items: Item[]
  defaultOpen: boolean
  empty: string
}

export function kindOf(item: Item): string {
  return partOf(item).label
}

// Klar för listorna och räknarna. isCompleted är också sann för en inlämnad uppgift (och för en som ska
// kompletteras), men en komplettering är ett steg kvar och räknas inte som klar. Inlämnad räknas.
export function isFinished(item: Item): boolean {
  return item.isCompleted && partStatusKey(item) !== 'submission.needsWork'
}

export function isAvailable(item: Item): boolean {
  return item.type !== 'News' && !item.isLocked && !isFinished(item)
}

export function isModuleComplete(module: Module): boolean {
  const parts = module.content.filter((item) => item.type !== 'News')
  return parts.length > 0 && parts.every(isFinished)
}

export function missingBlockers(item: Item): Schema<'StudentBlockerDto'>[] {
  return item.blockedBy.filter((blocker) => !blocker.isSatisfied)
}

export function sectionsFor(module: Module, filter: StudentFilter): PartSection[] {
  const available = module.content.filter(isAvailable)
  const availableIds = new Set(available.map((item) => item.linkId))
  const locked = module.content.filter((item) =>
    item.type !== 'News' && item.isLocked && !isFinished(item))

  // Bara låsta delar som öppnas när delarna i "Nästa steg" är klara. Då visas ett lager fram,
  // inte hela beroendekedjan.
  const unlocking = locked.filter((item) => {
    const missing = missingBlockers(item)
    return missing.length > 0 && missing.every((blocker) => availableIds.has(blocker.linkId))
  })
  const unlockingIds = new Set(unlocking.map((item) => item.linkId))
  const later = locked.filter((item) => !unlockingIds.has(item.linkId))

  const complete = isModuleComplete(module)
  const sections: PartSection[] = [
    {
      key: 'next',
      title: 'Nästa steg',
      items: available,
      defaultOpen: available.length > 0,
      empty: filter === 'submissions'
        ? 'Inga inlämningar att göra just nu.'
        : module.content.length === 0
          ? 'Inga publicerade delar i veckan än.'
          : 'Inga delar att göra just nu.',
    },
    {
      key: 'unlocking',
      title: 'Låses upp härnäst',
      items: unlocking,
      defaultOpen: unlocking.length > 0,
      empty: '',
    },
    {
      key: 'later',
      title: 'Låst senare',
      items: later,
      defaultOpen: false,
      empty: '',
    },
    {
      key: 'news',
      title: 'Nyheter',
      items: module.content.filter((item) => item.type === 'News'),
      defaultOpen: true,
      empty: '',
    },
    {
      key: 'completed',
      title: 'Klart',
      items: module.content.filter((item) => item.type !== 'News' && isFinished(item)),
      defaultOpen: false,
      empty: '',
    },
  ]

  // I en klar vecka är historiken viktigare än en tom "Nästa steg". Därför ligger Klart överst
  // och tomma sektioner visas inte. I en pågående vecka ligger Klart sist.
  const ordered = complete
    ? [sections[4], sections[0], sections[1], sections[2], sections[3]]
    : sections

  if (filter === 'todo') {
    return complete
      ? []
      : [sections[0]]
  }

  if (filter === 'submissions') {
    return ordered
      .map((section) => ({
        ...section,
        items: section.items.filter((item) => kindOf(item) === 'Inlämning'),
      }))
      .filter((section) => section.items.length > 0 || (section.key === 'next' && !complete))
  }

  return ordered.filter((section) =>
    section.items.length > 0 || (section.key === 'next' && !complete))
}

export function gradeScaleLabel(scale: Schema<'GradeScale'> | null): string | null {
  if (scale === 'PassFail') {
    return 'IG–G'
  }
  if (scale === 'PassFailDistinction') {
    return 'IG–VG'
  }
  return null
}

// Etiketten, ikonen och färgen kommer från samma läge (lib/status.ts), så de kan inte skilja sig åt.
export function statusKey(item: Item) {
  return partStatusKey(item)
}

export function rowIcon(item: Item): Component {
  return statuses[statusKey(item)].icon ?? PhCircle
}

export function rowIconClass(item: Item): string {
  return toneTextClasses[statuses[statusKey(item)].tone]
}

export function statusHeading(item: Item): string {
  return statuses[statusKey(item)].label
}

export function dueLabel(item: Item): string {
  return item.dueAt
    ? `Slutar ${formatDate(item.dueAt, true)}`
    : ''
}

// Klar eller inlämnad är inte försenad, så deadlinen får inget läge då.
function isDone(item: Item): boolean {
  return item.isCompleted || item.submissionStatus === 'Submitted' || item.submissionStatus === 'Graded'
}

export function dueStatus(item: Item) {
  return deadlineStatusKey(item.dueAt, isDone(item))
}
