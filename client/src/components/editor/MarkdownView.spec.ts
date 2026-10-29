import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'
import {defineComponent, h, ref} from 'vue'

import MarkdownView from './MarkdownView.vue'

// ProseMirror mäter markeringen när editorn får fokus, och jsdom har ingen layout.
Range.prototype.getClientRects = () => ({length: 0, item: () => null, [Symbol.iterator]: [][Symbol.iterator]})
Range.prototype.getBoundingClientRect = () => new DOMRect()

// En förälder med markdown i en ref, så att testet kan byta text utan att montera om.
function render(initial: string) {
  const markdown = ref(initial)
  const Parent = defineComponent({
    render: () => h(MarkdownView, {markdown: markdown.value}),
  })
  return {wrapper: mount(Parent, {attachTo: document.body}), markdown}
}

describe('MarkdownView', () => {
  it('visar markdown formaterat', async () => {
    const {wrapper} = render('Hej **alla**')
    await flushPromises()

    expect(wrapper.find('strong').text()).toBe('alla')
  })

  it('visar ==text== som understrykning', async () => {
    const {wrapper} = render('Gör ==det här== först')
    await flushPromises()

    expect(wrapper.find('mark').text()).toBe('det här')
  })

  it('visar en lista som punkter', async () => {
    const {wrapper} = render('- a\n- b')
    await flushPromises()

    expect(wrapper.findAll('li').map((item) => item.text())).toEqual(['a', 'b'])
  })

  it('skriver ut html i texten som text, inte som element', async () => {
    const {wrapper} = render('<img src=x onerror=alert(1)>\n\n<script>alert(1)</script>')
    await flushPromises()

    expect(wrapper.find('img').exists()).toBe(false)
    expect(wrapper.find('script').exists()).toBe(false)
  })

  it('byter text när markdown ändras', async () => {
    const {wrapper, markdown} = render('Först')
    await flushPromises()

    markdown.value = 'Sedan **igen**'
    await flushPromises()

    expect(wrapper.text()).toContain('Sedan')
    expect(wrapper.find('strong').text()).toBe('igen')
  })
})
