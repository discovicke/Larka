import {flushPromises, mount} from '@vue/test-utils'
import {describe, expect, it} from 'vitest'
import {defineComponent, h, ref} from 'vue'

import MarkdownEditor from './MarkdownEditor.vue'

// ProseMirror mäter markeringen när editorn får fokus, och jsdom har ingen layout.
Range.prototype.getClientRects = () => ({length: 0, item: () => null, [Symbol.iterator]: [][Symbol.iterator]})
Range.prototype.getBoundingClientRect = () => new DOMRect()

// En förälder med v-model, så att testet ser värdet editorn skickar ut.
function render(initial: string) {
  const value = ref(initial)
  const Parent = defineComponent({
    render: () => h(MarkdownEditor, {
      'id': 'body',
      'label': 'Beskrivning',
      'modelValue': value.value,
      'onUpdate:modelValue': (next: string) => {
        value.value = next
      },
    }),
  })
  return {wrapper: mount(Parent, {attachTo: document.body}), value}
}

function button(wrapper: ReturnType<typeof mount>, label: string) {
  const found = wrapper.findAll('button').find((candidate) => candidate.text() === label)
  if (!found) {
    throw new Error(`Hittar ingen knapp "${label}"`)
  }
  return found
}

describe('MarkdownEditor', () => {
  it('visar markdown formaterat', async () => {
    const {wrapper} = render('Hej **alla**')
    await flushPromises()

    expect(wrapper.find('[contenteditable] strong').text()).toBe('alla')
  })

  it('verktygsraden formaterar och värdet blir markdown', async () => {
    const {wrapper, value} = render('Hej')
    await flushPromises()

    await wrapper.find('[aria-label="Lista"]').trigger('click')

    expect(value.value.trim()).toBe('- Hej')
  })

  it('läser ==text== som understrykning och skriver tillbaka samma markdown', async () => {
    const {wrapper, value} = render('Gör ==det här== först')
    await flushPromises()

    expect(wrapper.find('[contenteditable] mark').text()).toBe('det här')

    await wrapper.find('[aria-label="Kursiv"]').trigger('click')
    expect(value.value).toContain('==det här==')
  })

  it('markdown-läget visar texten rå och ändringar följer med tillbaka', async () => {
    const {wrapper, value} = render('Hej **alla**')
    await flushPromises()

    await button(wrapper, 'Markdown').trigger('click')
    expect((wrapper.find('textarea').element as HTMLTextAreaElement).value).toBe('Hej **alla**')

    await wrapper.find('textarea').setValue('Hej *du*')
    await button(wrapper, 'Formaterad').trigger('click')

    expect(value.value).toBe('Hej *du*')
    expect(wrapper.find('[contenteditable] em').text()).toBe('du')
  })
})
