import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it, vi} from 'vitest'
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
  it('visar ett kodblock som ett block med språk, kopieringsknapp och färgad syntax', async () => {
    const {wrapper} = render('Före\n\n```html\n<form id="x">\n  <label>Rum</label>\n</form>\n```')
    await flushPromises()

    const blocks = wrapper.findAll('[data-code-block]')
    expect(blocks).toHaveLength(1)
    expect(blocks[0]!.find('[data-code-language]').text()).toBe('HTML')
    expect(blocks[0]!.findAll('pre')).toHaveLength(1)
    expect(blocks[0]!.find('pre code').text()).toBe('<form id="x">\n  <label>Rum</label>\n</form>')
    expect(blocks[0]!.find('pre code .hljs-name').exists()).toBe(true)
  })

  it('kopierar kodblockets text', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', {value: {writeText}, configurable: true})
    const {wrapper} = render('```csharp\nvar x = 1;\n```')
    await flushPromises()

    const button = wrapper.find('[data-code-block] button')
    expect(button.attributes('aria-label')).toBe('Kopiera C#-koden')
    await button.trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('var x = 1;')
    expect(button.text()).toBe('Kopierat')
  })

  it('visar ett kodblock utan språk som Kod', async () => {
    const {wrapper} = render('```\nnpm ci\n```')
    await flushPromises()

    expect(wrapper.find('[data-code-language]').text()).toBe('Kod')
  })

  it('låter en enkel radbrytning i ett stycke flyta ihop med texten', async () => {
    const {wrapper} = render('Ett ramverk som\nVue bygger ovanpå det.')
    await flushPromises()

    expect(wrapper.findAll('p')).toHaveLength(1)
    expect(wrapper.find('br').exists()).toBe(false)
    expect(wrapper.find('.ProseMirror').classes()).toContain('[&_p]:whitespace-normal')
  })
})
