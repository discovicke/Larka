<script setup lang="ts">
import {Markdown} from '@tiptap/markdown'
import StarterKit from '@tiptap/starter-kit'
import {EditorContent, useEditor} from '@tiptap/vue-3'
import {watch} from 'vue'

// Markdown som bara visas, med samma bibliotek som MarkdownEditor. Tiptap behåller bara de noder den
// känner till, så HTML i texten tas bort i stället för att hamna som element i sidan.
const props = defineProps<{markdown: string}>()

const editor = useEditor({
  extensions: [StarterKit, Markdown],
  content: props.markdown,
  contentType: 'markdown',
  editable: false,
  editorProps: {
    attributes: {
      class: 'outline-none leading-relaxed [&_p+*]:mt-3 [&_*+p]:mt-3 [&_ul]:list-disc [&_ul]:pl-5 '
        + '[&_ol]:list-decimal [&_ol]:pl-5 [&_li+li]:mt-1 [&_code]:rounded [&_code]:bg-muted [&_code]:px-1 '
        + '[&_code]:font-mono [&_a]:underline [&_a]:underline-offset-3 [&_strong]:font-semibold [&_h1]:text-xl '
        + '[&_h2]:text-lg [&_h3]:font-semibold [&_blockquote]:border-l-2 [&_blockquote]:pl-3 '
        + '[&_blockquote]:text-muted-foreground',
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
