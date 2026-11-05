<script lang="ts">
import {api, type Schema} from '@/lib/api'

type Person = Schema<'MentionableUserDto'>

// Listan hämtas första gången någon skriver @, och delas av alla fält på samma nyhet.
const peopleByContent = new Map<string, Promise<Person[]>>()

function loadPeople(contentId: string): Promise<Person[]> {
  let pending = peopleByContent.get(contentId)
  if (!pending) {
    pending = api.GET('/api/content/{id}/mentionable', {params: {path: {id: contentId}}})
      .then(({data}) => data ?? [])
      .catch(() => [])
    peopleByContent.set(contentId, pending)
  }
  return pending
}
</script>

<script setup lang="ts">
import {computed, nextTick, ref, useId} from 'vue'

import {Avatar, AvatarFallback, AvatarImage} from '@/components/ui/avatar'
import {Textarea} from '@/components/ui/textarea'
import {matchPeople, mentionQueryAt, type MentionMap} from '@/lib/mentions'
import {initials} from '@/lib/person'

const caretKeys = ['ArrowLeft', 'ArrowRight', 'Home', 'End']

const props = defineProps<{
  contentId: string
  id: string
  placeholder?: string
  rows?: number | string
}>()

// Texten som den står i fältet (@Namn), och vem varje namn är.
const text = defineModel<string>({required: true})
const mentions = defineModel<MentionMap>('mentions', {required: true})

const people = ref<Person[]>([])
const query = ref<{start: number, query: string} | null>(null)
const active = ref(0)
const field = ref<{$el: HTMLTextAreaElement} | null>(null)
const listId = useId()

const matches = computed(() => query.value
  ? matchPeople(people.value, query.value.query).slice(0, 8)
  : [])
const open = computed(() => matches.value.length > 0)

function optionId(index: number) {
  return `${listId}-option-${index}`
}

function onKeyup(event: KeyboardEvent) {
  if (caretKeys.includes(event.key)) {
    void updateQuery()
  }
}

// Fältets egen text och markör, inte modellen: vid input-händelsen har modellen inte hunnit uppdateras.
function queryIn(element: HTMLTextAreaElement) {
  return mentionQueryAt(element.value, element.selectionStart ?? element.value.length)
}

async function updateQuery() {
  const element = field.value?.$el
  if (!element) {
    return
  }
  if (queryIn(element) && people.value.length === 0) {
    people.value = await loadPeople(props.contentId)
  }
  const found = queryIn(element)
  if (found?.query !== query.value?.query || found?.start !== query.value?.start) {
    active.value = 0
  }
  query.value = found
}

async function pick(person: Person) {
  const element = field.value?.$el
  const current = query.value
  if (!element || !current) {
    return
  }
  const value = element.value
  const caret = element.selectionStart ?? value.length
  const inserted = `@${person.name} `
  text.value = value.slice(0, current.start) + inserted + value.slice(caret)
  mentions.value = new Map(mentions.value).set(person.name, person.id)
  query.value = null
  await nextTick()
  const position = current.start + inserted.length
  element.focus()
  element.setSelectionRange(position, position)
}

function onKeydown(event: KeyboardEvent) {
  if (!open.value) {
    return
  }
  switch (event.key) {
    case 'ArrowDown':
      active.value = (active.value + 1) % matches.value.length
      break
    case 'ArrowUp':
      active.value = (active.value - 1 + matches.value.length) % matches.value.length
      break
    case 'Enter':
    case 'Tab':
      void pick(matches.value[active.value]!)
      break
    case 'Escape':
      // Stänger listan, inte dialogen fältet kanske ligger i.
      event.stopPropagation()
      query.value = null
      break
    default:
      return
  }
  event.preventDefault()
}

// Ett klick utanför stänger listan. Klick i listan väljer innan fältet tappar fokus (mousedown.prevent).
function onBlur() {
  query.value = null
}
</script>

<template>
  <div class="relative">
    <Textarea
      :id="id"
      ref="field"
      v-model="text"
      :placeholder="placeholder"
      :rows="rows"
      role="combobox"
      aria-autocomplete="list"
      :aria-expanded="open"
      :aria-controls="open
        ? listId
        : undefined"
      :aria-activedescendant="open
        ? optionId(active)
        : undefined"
      data-test="mention-textarea"
      @input="updateQuery"
      @click="updateQuery"
      @keyup="onKeyup"
      @keydown="onKeydown"
      @blur="onBlur"
    />
    <ul
      v-if="open"
      :id="listId"
      role="listbox"
      aria-label="Nämn någon"
      class="bg-popover text-popover-foreground absolute inset-x-0 top-full z-50 mt-1 max-h-60 overflow-y-auto rounded-lg border p-1 shadow-md"
      data-test="mention-list"
    >
      <li
        v-for="(person, index) in matches"
        :id="optionId(index)"
        :key="person.id"
        role="option"
        :aria-selected="index === active"
        :class="[
          'flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm',
          index === active
            ? 'bg-accent text-accent-foreground'
            : '',
        ]"
        data-test="mention-option"
        @mousedown.prevent="pick(person)"
        @mousemove="active = index"
      >
        <Avatar class="size-6">
          <AvatarImage v-if="person.avatarUrl" :src="person.avatarUrl" alt=""/>
          <AvatarFallback class="text-[10px] font-semibold">{{ initials(person.name) }}</AvatarFallback>
        </Avatar>
        <span class="min-w-0 flex-1 truncate">{{ person.name }}</span>
        <span v-if="person.isTeacher" class="text-muted-foreground text-xs">Lärare</span>
      </li>
    </ul>
  </div>
</template>
