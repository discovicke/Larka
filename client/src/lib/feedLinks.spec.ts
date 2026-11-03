import {describe, expect, it} from 'vitest'

import {type FeedLink, linksByContent, linkTargets} from '@/lib/feedLinks'

function link(linkId: string, contentId: string, extra: Partial<FeedLink> = {}): FeedLink {
  return {linkId, contentId, courseId: null, groupId: null, ...extra}
}

describe('linksByContent', () => {
  it('grupperar per innehåll i den ordning innehållet syns och räknar samma länk en gång', () => {
    const items = [link('l1', 'c1'), link('l2', 'c2'), link('l1', 'c1'), link('l3', 'c1')]

    expect(linksByContent(items).map((group) => group.map((item) => item.linkId))).toEqual([['l1', 'l3'], ['l2']])
  })
})

describe('linkTargets', () => {
  it('ger kursnamnen först, sedan gruppnamnen, utan dubbletter och utan okända id:n', () => {
    const names = new Map([['k1', 'DevOps'], ['g1', '.NET25']])
    const links = [
      link('l1', 'c1', {groupId: 'g1'}),
      link('l2', 'c1', {courseId: 'k1'}),
      link('l3', 'c1', {courseId: 'k1', groupId: 'okänd'}),
    ]

    expect(linkTargets(links, names)).toEqual(['DevOps', '.NET25'])
  })
})
