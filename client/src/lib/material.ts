import {PhFile, PhFileDoc, PhFileImage, PhFilePdf, PhFilePpt, PhFileXls, PhFileZip} from '@phosphor-icons/vue'
import type {Component} from 'vue'

import type {Schema} from '@/lib/api'
import {linksByContent, linkTargets} from '@/lib/feedLinks'

/** Ikon för filens ändelse, med en allmän filikon för allt annat. */
const fileIcons: Record<string, Component> = {
  pdf: PhFilePdf,
  doc: PhFileDoc,
  docx: PhFileDoc,
  xls: PhFileXls,
  xlsx: PhFileXls,
  csv: PhFileXls,
  ppt: PhFilePpt,
  pptx: PhFilePpt,
  png: PhFileImage,
  jpg: PhFileImage,
  jpeg: PhFileImage,
  gif: PhFileImage,
  webp: PhFileImage,
  zip: PhFileZip,
}

export function fileIcon(fileName: string): Component {
  const extension = fileName.split('.').pop()?.toLowerCase() ?? ''
  return fileIcons[extension] ?? PhFile
}

/** Det som behövs från ett flödesobjekt för att gruppera materialet. */
export type MaterialLink = Pick<Schema<'ContentFeedItem'>, 'linkId' | 'contentId' | 'title' | 'authorName' | 'courseId' | 'groupId' | 'publishAt'>

export interface MaterialRow {
  attachment: Schema<'AttachmentDto'>
  title: string
  targets: string[]
  authorName: string
  publishAt: string
}

function earliest(links: MaterialLink[]): string {
  return links
    .map((link) => link.publishAt)
    .reduce((first, current) => Date.parse(current) < Date.parse(first)
      ? current
      : first)
}

/**
 * En rad per bilaga. Flödena innehåller samma länk både från kursen och från gruppen,
 * så länkar som setts förut hoppas över. Målen är kursnamn först och sedan gruppnamn.
 * Bilagor vars innehåll inte finns i flödena visas inte.
 */
export function materialRows(
  items: MaterialLink[],
  attachments: Schema<'AttachmentDto'>[],
  names: Map<string, string>,
): MaterialRow[] {
  const contents = new Map<string, Omit<MaterialRow, 'attachment'>>()
  for (const links of linksByContent(items)) {
    const first = links[0]
    contents.set(first.contentId, {
      title: first.title,
      authorName: first.authorName,
      targets: linkTargets(links, names),
      publishAt: earliest(links),
    })
  }

  return attachments
    .flatMap((attachment) => {
      const content = contents.get(attachment.contentId)
      return content
        ? [{attachment, ...content}]
        : []
    })
    .sort((left, right) => Date.parse(right.publishAt) - Date.parse(left.publishAt))
}

/** Sant om filen laddades upp under de senaste sju dygnen. */
export function isThisWeek(iso: string, now: Date): boolean {
  const days = (now.getTime() - new Date(iso).getTime()) / 86_400_000
  return days >= 0 && days <= 7
}

/** "512 B", "2,0 kB" eller "1,5 MB", med svenskt komma. */
export function formatSize(bytes: number): string {
  if (bytes < 1000) {
    return `${bytes} B`
  }
  if (bytes < 1_000_000) {
    return `${(bytes / 1000).toFixed(1).replace('.', ',')} kB`
  }
  return `${(bytes / 1_000_000).toFixed(1).replace('.', ',')} MB`
}
