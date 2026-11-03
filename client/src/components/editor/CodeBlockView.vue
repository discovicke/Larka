<script setup lang="ts">
import {PhCheck, PhCopy} from '@phosphor-icons/vue'
import {NodeViewContent, nodeViewProps, NodeViewWrapper} from '@tiptap/vue-3'
import {computed, onBeforeUnmount, ref} from 'vue'

import {Button} from '@/components/ui/button'
import {languageLabel} from '@/lib/code-highlight'

// Kodblock i MarkdownView: en rubrik med språk och kopieringsknapp ovanför koden.
const props = defineProps(nodeViewProps)

const label = computed(() => languageLabel(props.node.attrs.language as string | null))
const copied = ref(false)
const copyLabel = computed(() => copied.value
  ? 'Kopierat'
  : 'Kopiera')
const copyIcon = computed(() => copied.value
  ? PhCheck
  : PhCopy)
const copyAriaLabel = computed(() => copied.value
  ? 'Kopierat'
  : `Kopiera ${label.value}-koden`)
let reset: ReturnType<typeof setTimeout> | undefined

async function copy() {
  try {
    await navigator.clipboard.writeText(props.node.textContent)
    copied.value = true
    clearTimeout(reset)
    reset = setTimeout(() => {
      copied.value = false
    }, 2000)
  } catch {
    // Utan behörighet till urklipp går koden fortfarande att markera och kopiera.
    copied.value = false
  }
}

onBeforeUnmount(() => clearTimeout(reset))
</script>

<template>
  <NodeViewWrapper class="code-block bg-muted/50 overflow-hidden rounded-lg border" data-code-block>
    <div class="text-muted-foreground flex items-center justify-between border-b py-1 pr-1 pl-3 text-xs" contenteditable="false">
      <span data-code-language>{{ label }}</span>
      <Button
        type="button"
        variant="ghost"
        size="xs"
        :aria-label="copyAriaLabel"
        @click="copy"
      >
        <component :is="copyIcon"/>
        <span aria-live="polite">{{ copyLabel }}</span>
      </Button>
    </div>
    <pre class="overflow-x-auto px-4 py-3 font-mono text-[13px] leading-[1.6] whitespace-pre!"><NodeViewContent as="code" class="hljs whitespace-pre!"/></pre>
  </NodeViewWrapper>
</template>
