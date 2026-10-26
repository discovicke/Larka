<script setup lang="ts">
import {PhCode, PhListBullets, PhPaperclip, PhTextB, PhTextItalic} from '@phosphor-icons/vue'
import type {Editor} from '@tiptap/core'
import {Markdown} from '@tiptap/markdown'
import StarterKit from '@tiptap/starter-kit'
import {EditorContent, useEditor} from '@tiptap/vue-3'
import {ref} from 'vue'

import {Button} from '@/components/ui/button'
import {Textarea} from '@/components/ui/textarea'

// Läraren skriver formaterat (Tiptap) eller i markdown (textarea). Värdet är alltid markdown, så backend
// sparar samma format oavsett läge.
const props = defineProps<{id: string, label: string}>()
const model = defineModel<string>({required: true})

const mode = ref<'rich' | 'markdown'>('rich')

const editor = useEditor({
  extensions: [StarterKit, Markdown],
  content: model.value,
  contentType: 'markdown',
  editorProps: {
    attributes: {
      'aria-label': props.label,
      'class': 'min-h-28 px-3 py-2 outline-none [&_p+*]:mt-2 [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal '
        + '[&_ol]:pl-5 [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 [&_code]:font-mono',
    },
  },
  onUpdate: ({editor: current}) => {
    model.value = current.getMarkdown()
  },
})

const tools = [
  {label: 'Fetstil', icon: PhTextB, name: 'bold', run: (current: Editor) => current.chain().focus().toggleBold().run()},
  {label: 'Kursiv', icon: PhTextItalic, name: 'italic', run: (current: Editor) => current.chain().focus().toggleItalic().run()},
  {label: 'Kod', icon: PhCode, name: 'code', run: (current: Editor) => current.chain().focus().toggleCode().run()},
  {label: 'Lista', icon: PhListBullets, name: 'bulletList', run: (current: Editor) => current.chain().focus().toggleBulletList().run()},
]

// Markdown som skrivits i textarean läses in i editorn igen.
function showRich() {
  editor.value?.commands.setContent(model.value, {contentType: 'markdown'})
  mode.value = 'rich'
}
</script>

<template>
  <div class="focus-within:border-ring overflow-hidden rounded-lg border">
    <div class="flex items-center gap-0.5 border-b bg-stone-50 p-1">
      <template v-if="editor && mode === 'rich'">
        <Button
          v-for="tool in tools"
          :key="tool.name"
          type="button"
          variant="ghost"
          size="icon-sm"
          :aria-label="tool.label"
          :aria-pressed="editor.isActive(tool.name)"
          class="aria-pressed:bg-muted"
          @click="tool.run(editor)"
        >
          <component :is="tool.icon"/>
        </Button>
      </template>
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        aria-label="Bifoga fil"
        title="Filuppladdning kommer i #47"
        disabled
      >
        <PhPaperclip/>
      </Button>

      <div class="ml-auto flex gap-0.5">
        <Button
          type="button"
          variant="ghost"
          size="xs"
          :aria-pressed="mode === 'rich'"
          class="aria-pressed:bg-muted"
          @click="showRich"
        >
          Formaterad
        </Button>
        <Button
          type="button"
          variant="ghost"
          size="xs"
          :aria-pressed="mode === 'markdown'"
          class="aria-pressed:bg-muted"
          @click="mode = 'markdown'"
        >
          Markdown
        </Button>
      </div>
    </div>

    <EditorContent v-show="mode === 'rich'" :editor="editor"/>
    <Textarea
      v-if="mode === 'markdown'"
      :id="id"
      v-model="model"
      :aria-label="label"
      class="min-h-28 rounded-none border-0 font-mono text-[13px] shadow-none focus-visible:ring-0"
    />
  </div>
</template>
