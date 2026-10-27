<script setup lang="ts">
import {
  PhArrowDown,
  PhArrowUp,
  PhCaretDown,
  PhDotsThree,
  PhEye,
  PhEyeSlash,
  PhInfo,
  PhLock,
  PhLockOpen,
  PhPencilSimple,
  PhPlus,
  PhTrash,
  PhTreeStructure,
  PhUsersThree,
} from '@phosphor-icons/vue'
import {ref} from 'vue'

import {Badge} from '@/components/ui/badge'
import {Button} from '@/components/ui/button'
import {Collapsible, CollapsibleContent, CollapsibleTrigger} from '@/components/ui/collapsible'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {Item, ItemActions, ItemContent, ItemDescription, ItemMedia, ItemTitle} from '@/components/ui/item'
import type {Schema} from '@/lib/api'
import {formatDate, partOf} from '@/lib/content'

const props = defineProps<{
  module: Schema<'ModuleWithContentDto'>
  canMoveUp: boolean
  canMoveDown: boolean
}>()

const emit = defineEmits<{
  move: [direction: -1 | 1]
  openRoadmap: []
  movePart: [item: Schema<'ModuleContentItem'>, direction: -1 | 1]
  togglePartStatus: [item: Schema<'ModuleContentItem'>]
  deletePart: [item: Schema<'ModuleContentItem'>]
}>()

const open = ref(true)

function lockedByCount(item: Schema<'ModuleContentItem'>): number {
  return (item.blockedBy ?? []).length
}

function unlockCountOf(item: Schema<'ModuleContentItem'>): number {
  return props.module.content
    .filter((candidate) => (candidate.blockedBy ?? []).includes(item.linkId))
    .length
}

function editRoute(item: Schema<'ModuleContentItem'>) {
  return {
    name: 'edit-content',
    params: {courseId: props.module.courseId, moduleId: props.module.id, linkId: item.linkId},
  }
}

function detailsOf(item: Schema<'ModuleContentItem'>): string {
  const label = partOf(item).label
  return item.dueAt
    ? `${label} · ${formatDate(item.dueAt)}`
    : label
}

function badgeOf(item: Schema<'ModuleContentItem'>): {label: string, class: string} {
  if (item.status === 'Draft') {
    return {label: 'Utkast', class: ''}
  }
  if (new Date(item.publishAt) > new Date()) {
    return {label: `Publiceras ${formatDate(item.publishAt, true)}`, class: 'bg-muted'}
  }
  return {label: 'Publicerad', class: 'border-emerald-200 bg-emerald-50 text-emerald-700'}
}
</script>

<template>
  <Collapsible
    v-model:open="open"
    as="section"
    :aria-label="module.title"
    class="overflow-hidden rounded-[11px] border"
  >
    <div class="flex items-center gap-2.5 bg-stone-50 py-2 pr-3 pl-2" :class="{'border-b': open}">
      <CollapsibleTrigger as-child>
        <Button
          variant="ghost"
          size="icon-sm"
          :aria-label="open
            ? 'Fäll ihop'
            : 'Fäll ut'"
        >
          <PhCaretDown class="text-muted-foreground transition-transform" :class="{'-rotate-90': !open}"/>
        </Button>
      </CollapsibleTrigger>
      <h2 class="flex-1 font-sans text-sm font-semibold">{{ module.title }}</h2>
      <span class="text-muted-foreground text-[13px]">{{ module.content.length }} delar</span>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button variant="ghost" size="icon" aria-label="Fler val för veckan">
            <PhDotsThree/>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem :disabled="!canMoveUp" @select="emit('move', -1)">
            <PhArrowUp/>
            Flytta upp
          </DropdownMenuItem>
          <DropdownMenuItem :disabled="!canMoveDown" @select="emit('move', 1)">
            <PhArrowDown/>
            Flytta ner
          </DropdownMenuItem>
          <DropdownMenuSeparator/>
          <DropdownMenuItem @select="emit('openRoadmap')">
            <PhTreeStructure/>
            Roadmap
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>

    <CollapsibleContent>
      <p v-if="module.description" class="text-muted-foreground flex items-center gap-1.5 px-4 pt-2.5 pb-1.5 pl-10 text-[13px]">
        <PhInfo class="size-3.5"/>
        {{ module.description }}
      </p>

      <Item
        v-for="(item, index) in module.content"
        :key="item.linkId"
        size="sm"
        class="rounded-none border-0 border-t border-t-stone-100 pl-3"
        data-test="module-item"
      >
        <ItemMedia variant="icon" class="text-muted-foreground">
          <component :is="partOf(item).icon"/>
        </ItemMedia>
        <ItemContent class="min-w-60">
          <ItemTitle class="font-normal">
            <RouterLink
              v-if="item.type !== 'News'"
              :to="editRoute(item)"
              class="hover:underline"
            >
              {{ item.title }}
            </RouterLink>
            <template v-else>{{ item.title }}</template>
          </ItemTitle>
          <div
            v-if="lockedByCount(item) > 0 || unlockCountOf(item) > 0"
            class="text-muted-foreground mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-[12px]"
          >
            <span v-if="lockedByCount(item) > 0" class="flex items-center gap-1">
              <PhLock class="size-3"/>
              Låst av {{ lockedByCount(item) }} {{ lockedByCount(item) === 1
                ? 'del'
                : 'delar' }}
            </span>
            <span v-if="unlockCountOf(item) > 0" class="flex items-center gap-1">
              <PhLockOpen class="size-3"/>
              Låser upp {{ unlockCountOf(item) }} {{ unlockCountOf(item) === 1
                ? 'del'
                : 'delar' }}
            </span>
          </div>
        </ItemContent>
        <ItemDescription class="text-[13px]">{{ detailsOf(item) }}</ItemDescription>
        <ItemActions>
          <Badge variant="outline" class="h-[22px] rounded-[5px]" :class="badgeOf(item).class">
            {{ badgeOf(item).label }}
          </Badge>
          <DropdownMenu>
            <DropdownMenuTrigger as-child>
              <Button variant="ghost" size="icon-sm" :aria-label="`Fler val för ${item.title}`">
                <PhDotsThree/>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem v-if="item.type !== 'News'" as-child>
                <RouterLink :to="editRoute(item)">
                  <PhPencilSimple/>
                  Redigera
                </RouterLink>
              </DropdownMenuItem>
              <DropdownMenuItem v-if="item.type === 'Assignment'" as-child>
                <RouterLink
                  :to="{
                    name: 'assignment-submissions',
                    params: {courseId: module.courseId, linkId: item.linkId},
                  }"
                >
                  <PhUsersThree/>
                  Inlämningar
                </RouterLink>
              </DropdownMenuItem>
              <DropdownMenuItem @select="emit('togglePartStatus', item)">
                <PhEye v-if="item.status === 'Draft'"/>
                <PhEyeSlash v-else/>
                {{ item.status === 'Draft'
                  ? 'Publicera'
                  : 'Avpublicera' }}
              </DropdownMenuItem>
              <DropdownMenuItem :disabled="index === 0" @select="emit('movePart', item, -1)">
                <PhArrowUp/>
                Flytta upp
              </DropdownMenuItem>
              <DropdownMenuItem
                :disabled="index === module.content.length - 1"
                @select="emit('movePart', item, 1)"
              >
                <PhArrowDown/>
                Flytta ner
              </DropdownMenuItem>
              <DropdownMenuItem variant="destructive" @select="emit('deletePart', item)">
                <PhTrash/>
                Ta bort
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </ItemActions>
      </Item>

      <div class="border-t border-t-stone-100 px-3 pt-2 pb-3 pl-10">
        <!-- Typen väljs i formuläret. -->
        <Button
          variant="ghost"
          size="sm"
          class="text-stone-700"
          as-child
        >
          <RouterLink
            :to="{name: 'create-content', params: {courseId: module.courseId, moduleId: module.id}}"
            data-test="add-part"
          >
            <PhPlus/>
            Lägg till del
          </RouterLink>
        </Button>
      </div>
    </CollapsibleContent>
  </Collapsible>
</template>
