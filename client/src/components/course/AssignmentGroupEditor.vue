<script setup lang="ts">
import {PhArrowsClockwise, PhPlus, PhTrash} from '@phosphor-icons/vue'
import {computed, ref, watch} from 'vue'

import {Button} from '@/components/ui/button'
import {FieldError} from '@/components/ui/field'
import {Input} from '@/components/ui/input'
import {NativeSelect, NativeSelectOption} from '@/components/ui/native-select'
import type {Schema} from '@/lib/api'
import type {GroupForm} from '@/lib/createContent'

const props = defineProps<{
  students: Schema<'CourseStudentDto'>[]
  loading?: boolean
  error?: string
  lockedCount?: boolean
}>()

const groups = defineModel<GroupForm[]>({required: true})
const groupCount = ref(2)

watch(() => [props.lockedCount, groups.value.length] as const, ([locked, length]) => {
  if (locked) {
    groupCount.value = Math.max(1, length)
  }
}, {immediate: true})

const unassigned = computed(() => props.students.filter((student) => groupIndexOf(student.id) < 0))

function groupIndexOf(studentId: string): number {
  return groups.value.findIndex((group) => group.studentIds.includes(studentId))
}

function membersOf(index: number): Schema<'CourseStudentDto'>[] {
  const ids = new Set(groups.value[index]?.studentIds ?? [])
  return props.students.filter((student) => ids.has(student.id))
}

function moveStudent(studentId: string, groupIndex: number) {
  const next = groups.value.map((group) => ({
    ...group,
    studentIds: group.studentIds.filter((id) => id !== studentId),
  }))
  if (groupIndex >= 0 && groupIndex < next.length) {
    next[groupIndex].studentIds.push(studentId)
  }
  groups.value = next
}

function randomize() {
  if (props.students.length === 0) {
    return
  }

  const count = Math.max(1, Math.min(groupCount.value, props.students.length))
  const shuffled = [...props.students]
  for (let index = shuffled.length - 1; index > 0; index--) {
    const swap = Math.floor(Math.random() * (index + 1))
    ;[shuffled[index], shuffled[swap]] = [shuffled[swap], shuffled[index]]
  }

  const next: GroupForm[] = Array.from({length: count}, (_, index) => ({
    name: `Grupp ${index + 1}`,
    studentIds: [],
  }))
  shuffled.forEach((student, index) => {
    next[index % count].studentIds.push(student.id)
  })
  groups.value = next
}

function addGroup() {
  groups.value = [
    ...groups.value,
    {name: `Grupp ${groups.value.length + 1}`, studentIds: []},
  ]
}

function removeGroup(index: number) {
  groups.value = groups.value.filter((_, candidate) => candidate !== index)
}
</script>

<template>
  <div class="flex flex-col gap-3" data-test="group-editor">
    <div class="flex flex-wrap items-end justify-between gap-2">
      <div class="flex flex-col gap-0.5">
        <h3 class="text-sm font-semibold">Grupper</h3>
        <p class="text-muted-foreground text-xs">
          Slumpa fram grupper och flytta elever mellan dem innan du sparar.
        </p>
      </div>
      <div class="flex flex-wrap items-end gap-2">
        <label class="flex flex-col gap-1 text-xs">
          Antal grupper
          <Input
            v-model.number="groupCount"
            type="number"
            min="1"
            :max="Math.max(1, students.length)"
            :disabled="lockedCount"
            class="h-8 w-20"
            data-test="group-count"
          />
        </label>
        <Button
          type="button"
          variant="outline"
          size="sm"
          :disabled="loading || students.length === 0"
          data-test="group-randomize"
          @click="randomize"
        >
          <PhArrowsClockwise/>
          Slumpa grupper
        </Button>
      </div>
    </div>

    <p v-if="loading" class="text-muted-foreground text-xs">Hämtar kursens elever…</p>
    <p v-else-if="students.length === 0" class="text-muted-foreground text-xs">
      Kursen har inga elever att dela in.
    </p>

    <div v-else class="grid gap-2 md:grid-cols-2">
      <div
        v-for="(group, index) in groups"
        :key="index"
        class="flex flex-col gap-2 rounded-lg border p-3"
        data-test="group-card"
      >
        <div class="flex items-center gap-2">
          <Input
            v-model="group.name"
            :aria-label="`Namn på grupp ${index + 1}`"
            data-test="group-name"
          />
          <Button
            v-if="!lockedCount"
            type="button"
            variant="ghost"
            size="icon-sm"
            :aria-label="`Ta bort grupp ${index + 1}`"
            data-test="group-remove"
            @click="removeGroup(index)"
          >
            <PhTrash/>
          </Button>
        </div>
        <ul class="flex flex-col gap-1 text-xs" data-test="group-members">
          <li v-for="student in membersOf(index)" :key="student.id">
            {{ student.name }}
            <span class="text-muted-foreground">· {{ student.email }}</span>
          </li>
          <li v-if="membersOf(index).length === 0" class="text-muted-foreground">
            Inga elever än.
          </li>
        </ul>
      </div>
    </div>

    <Button
      v-if="students.length > 0 && !lockedCount"
      type="button"
      variant="outline"
      size="sm"
      class="w-fit"
      data-test="group-add"
      @click="addGroup"
    >
      <PhPlus/>
      Lägg till grupp
    </Button>

    <div v-if="students.length > 0" class="flex flex-col gap-2" data-test="group-assignments">
      <h4 class="text-xs font-semibold">Alla elever</h4>
      <div
        v-for="student in students"
        :key="student.id"
        class="grid items-center gap-2 rounded-md border px-2.5 py-1.5 text-xs sm:grid-cols-[minmax(0,1fr)_180px]"
      >
        <span class="min-w-0 truncate">
          {{ student.name }}
          <span class="text-muted-foreground">· {{ student.email }}</span>
        </span>
        <NativeSelect
          :model-value="String(groupIndexOf(student.id))"
          :aria-label="`Grupp för ${student.name}`"
          data-test="student-group"
          @update:model-value="moveStudent(student.id, Number($event))"
        >
          <NativeSelectOption value="-1">Inte tilldelad</NativeSelectOption>
          <NativeSelectOption
            v-for="(group, index) in groups"
            :key="index"
            :value="String(index)"
          >
            {{ group.name || `Grupp ${index + 1}` }}
          </NativeSelectOption>
        </NativeSelect>
      </div>
    </div>

    <p v-if="unassigned.length > 0" class="text-warning text-xs" data-test="group-unassigned">
      {{ unassigned.length }}
      {{ unassigned.length === 1
        ? 'elev saknar'
        : 'elever saknar' }}
      grupp.
    </p>
    <FieldError v-if="error" data-test="group-error">{{ error }}</FieldError>
  </div>
</template>
