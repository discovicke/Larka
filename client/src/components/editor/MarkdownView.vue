<script setup lang="ts">
import CodeBlockLowlight from '@tiptap/extension-code-block-lowlight'
import Highlight from '@tiptap/extension-highlight'
import {Markdown} from '@tiptap/markdown'
import StarterKit from '@tiptap/starter-kit'
import {EditorContent, useEditor, VueNodeViewRenderer} from '@tiptap/vue-3'
import {watch} from 'vue'

import CodeBlockView from '@/components/editor/CodeBlockView.vue'
import {lowlight} from '@/lib/code-highlight'

// Markdown som bara visas, med samma bibliotek som MarkdownEditor. Tiptap behåller bara de noder den
// känner till, så HTML i texten tas bort i stället för att hamna som element i sidan.
const props = defineProps<{markdown: string}>()

// Kodblock med färgad syntax och en rubrik med språk och kopieringsknapp.
const CodeBlock = CodeBlockLowlight
  .extend({addNodeView: () => VueNodeViewRenderer(CodeBlockView)})
  .configure({lowlight, defaultLanguage: null})

const editor = useEditor({
  extensions: [StarterKit.configure({codeBlock: false}), CodeBlock, Highlight, Markdown],
  content: props.markdown,
  contentType: 'markdown',
  editable: false,
  editorProps: {
    attributes: {
      // Avstånd mellan alla block (stycken, listor, rubriker), lite mer före en rubrik.
      // ProseMirror visar radbrytningar som de står (pre-wrap). I ett stycke är en enkel radbrytning i
      // markdown bara ett mellanslag, så stycken får vanlig radbrytning. Kodblock behåller sina rader.
      class: 'outline-none leading-relaxed [&>*+*]:mt-3 [&>*+h1]:mt-6 [&>*+h2]:mt-5 [&>*+h3]:mt-4 '
        + '[&_p]:whitespace-normal '
        + '[&_li>p+p]:mt-2 [&_ul]:list-disc [&_ul]:pl-5 '
        + '[&_ol]:list-decimal [&_ol]:pl-5 [&_li+li]:mt-1 '
        + '[&_:not(pre)>code]:rounded [&_:not(pre)>code]:bg-muted [&_:not(pre)>code]:px-1 [&_:not(pre)>code]:font-mono [&_:not(pre)>code]:text-[0.9em] '
        + '[&_a]:underline [&_a]:underline-offset-3 [&_strong]:font-semibold [&_h1]:text-xl '
        + '[&_h1]:font-semibold [&_h2]:text-lg [&_h2]:font-semibold [&_h3]:font-semibold '
        + '[&_blockquote]:border-l-2 [&_blockquote]:pl-3 '
        + '[&_blockquote]:text-muted-foreground '
        + '[&_mark]:bg-transparent [&_mark]:text-inherit [&_mark]:font-[inherit] [&_mark]:underline [&_mark]:decoration-primary [&_mark]:decoration-[3px] [&_mark]:underline-offset-[3px]',
    },
  },
})

// Nyheten kan bytas ut utan att komponenten monteras om.
watch(() => props.markdown, (markdown) => {
  editor.value?.commands.setContent(markdown, {contentType: 'markdown'})
})
</script>

<template>
  <EditorContent :editor="editor"/>
</template>
