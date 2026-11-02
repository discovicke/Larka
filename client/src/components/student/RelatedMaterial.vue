<script setup lang="ts">
import {PhArrowRight, PhBookOpen} from '@phosphor-icons/vue'
import {ref} from 'vue'

import MarkdownView from '@/components/editor/MarkdownView.vue'
import {Button} from '@/components/ui/button'
import {Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle} from '@/components/ui/sheet'
import {Skeleton} from '@/components/ui/skeleton'
import {useAsync} from '@/composables/useAsync'
import {type Schema} from '@/lib/api'
import {fetchContent} from '@/lib/contentApi'

const props = defineProps<{
  links: Schema<'RelatedLinkDto'>[]
}>()

const open = ref(false)
const active = ref<Schema<'RelatedLinkDto'> | null>(null)
const content = ref<Schema<'ContentDto'> | null>(null)

const {run: loadContent, loading, error} = useAsync(async (contentId: string) => {
  content.value = await fetchContent(contentId)
}, 'Kunde inte hämta teoridelen.')

async function openLink(link: Schema<'RelatedLinkDto'>) {
  active.value = link
  content.value = null
  open.value = true
  await loadContent(link.contentId)
}
</script>

<template>
  <section
    v-if="props.links.length > 0"
    class="bg-card flex flex-col gap-3 rounded-xl border p-4"
    aria-labelledby="related-material-h"
    data-test="related-material"
  >
    <div class="flex flex-col gap-0.5">
      <h2 id="related-material-h" class="text-sm font-semibold">Relaterat material</h2>
      <p class="text-muted-foreground text-xs">
        Teoridelar som hör ihop med uppgiften. De öppnas här utan att du lämnar sidan.
      </p>
    </div>

    <ul class="flex flex-col gap-2">
      <li v-for="link in props.links" :key="link.linkId">
        <button
          type="button"
          class="hover:bg-muted/40 flex w-full items-center gap-3 rounded-lg border px-3 py-2 text-left transition-colors"
          data-test="related-open"
          @click="openLink(link)"
        >
          <PhBookOpen class="text-muted-foreground size-4 shrink-0"/>
          <span class="flex min-w-0 flex-1 flex-col gap-0.5">
            <span class="text-sm font-medium">{{ link.title }}</span>
            <span v-if="link.moduleTitle" class="text-muted-foreground text-xs">{{ link.moduleTitle }}</span>
          </span>
          <PhArrowRight class="text-muted-foreground size-4 shrink-0"/>
        </button>
      </li>
    </ul>

    <Sheet v-model:open="open">
      <SheetContent side="right" class="w-full sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>{{ active?.title ?? 'Relaterat material' }}</SheetTitle>
          <SheetDescription v-if="active?.moduleTitle">{{ active.moduleTitle }}</SheetDescription>
        </SheetHeader>

        <div class="min-h-0 flex-1 overflow-y-auto pr-1" data-test="related-sheet">
          <Skeleton v-if="loading" class="h-40 w-full"/>
          <p v-else-if="error" class="text-destructive text-sm">{{ error }}</p>
          <MarkdownView
            v-else-if="content?.bodyMd"
            :markdown="content.bodyMd"
            class="text-sm"
            data-test="related-body"
          />
          <p v-else class="text-muted-foreground text-sm">Teoridelen har ingen text.</p>
        </div>

        <div class="border-t pt-3">
          <Button type="button" variant="outline" @click="open = false">Stäng</Button>
        </div>
      </SheetContent>
    </Sheet>
  </section>
</template>
