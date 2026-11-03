<script setup lang="ts">
import {PhCode, PhHighlighterCircle, PhListBullets, PhPaperclip, PhTextB, PhTextItalic} from '@phosphor-icons/vue'
import type {Editor} from '@tiptap/core'
import Highlight from '@tiptap/extension-highlight'
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
  extensions: [StarterKit, Highlight, Markdown],
  content: model.value,
  contentType: 'markdown',
  editorProps: {
    attributes: {
      'aria-label': props.label,
      'class': 'min-h-28 px-3 py-2 outline-none [&>*+*]:mt-2 [&>*+h1]:mt-4 [&>*+h2]:mt-4 [&>*+h3]:mt-3 [&_ul]:list-disc [&_ul]:pl-5 [&_ol]:list-decimal '
        + '[&_ol]:pl-5 [&_:not(pre)>code]:rounded [&_:not(pre)>code]:bg-muted [&_:not(pre)>code]:px-1 [&_:not(pre)>code]:font-mono '
        + '[&_pre]:rounded-lg [&_pre]:border [&_pre]:bg-muted/50 [&_pre]:p-3 [&_pre]:overflow-x-auto [&_pre]:font-mono [&_pre]:text-[13px] [&_pre]:leading-6 '
        + '[&_mark]:bg-transparent [&_mark]:text-inherit [&_mark]:font-[inherit] [&_mark]:underline [&_mark]:decoration-primary [&_mark]:decoration-[3px] [&_mark]:underline-offset-[3px]',
    },
  },
  onUpdate: ({editor: current}) => {
    model.value = current.getMarkdown()
  },
})

const tools = [
  {label: 'Fetstil', icon: PhTextB, name: 'bold', run: (current: Editor) => current.chain().focus().toggleBold().run()},
  {label: 'Kursiv', icon: PhTextItalic, name: 'italic', run: (current: Editor) => current.chain().focus().toggleItalic().run()},
  {label: 'Understrykning', icon: PhHighlighterCircle, name: 'highlight', run: (current: Editor) => current.chain().focus().toggleHighlight().run()},
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
    <div class="bg-muted/40 flex items-center gap-0.5 border-b p-1">
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
