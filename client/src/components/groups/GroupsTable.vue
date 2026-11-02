<script setup lang="ts">
import {PhDotsThree, PhPencilSimple, PhTrash} from '@phosphor-icons/vue'

import CourseStatusBadge from '@/components/course/CourseStatusBadge.vue'
import {Button} from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import type {GroupRow} from '@/lib/groupRows'

const props = withDefaults(defineProps<{
  rows: GroupRow[]
  isAdmin: boolean
  emptyText?: string
}>(), {emptyText: 'Inga grupper matchar sökningen.'})

const emit = defineEmits<{
  edit: [row: GroupRow]
  delete: [row: GroupRow]
}>()

function rowBorder(index: number) {
  return index < props.rows.length - 1
    ? 'border-b'
    : ''
}
</script>

<template>
  <section aria-label="Grupper" class="bg-card overflow-hidden rounded-[11px] border">
    <div class="overflow-x-auto">
      <table class="w-full border-collapse max-md:block md:min-w-[680px]">
        <thead class="max-md:hidden">
          <tr class="bg-muted/50 text-muted-foreground text-left text-[13px]">
            <th scope="col" class="border-b px-4 py-2.5 font-medium">Grupp</th>
            <th scope="col" class="border-b px-3 py-2.5 font-medium">Kod</th>
            <th scope="col" class="border-b px-3 py-2.5 font-medium">Typ</th>
            <th scope="col" class="border-b px-3 py-2.5 font-medium">Status</th>
            <th scope="col" class="border-b px-4 py-2.5 font-medium">Period</th>
            <th v-if="isAdmin" scope="col" class="border-b px-4 py-2.5 text-right font-medium">
              <span class="sr-only">Åtgärder</span>
            </th>
          </tr>
        </thead>
        <tbody class="max-md:block">
          <tr
            v-for="(row, index) in rows"
            :key="row.key"
            class="max-md:relative max-md:flex max-md:flex-wrap max-md:items-center max-md:gap-x-3 max-md:gap-y-1 max-md:px-4 max-md:py-3"
            :class="index < rows.length - 1
              ? 'max-md:border-b'
              : ''"
            data-test="group-row"
          >
            <td class="px-4 py-3 max-md:w-full max-md:border-b-0 max-md:p-0 max-md:pr-10" :class="rowBorder(index)">
              <RouterLink
                v-if="row.to"
                :to="row.to"
                class="font-medium underline underline-offset-3 decoration-border hover:decoration-foreground"
              >
                {{ row.name }}
              </RouterLink>
              <span v-else class="font-medium">{{ row.name }}</span>
            </td>
            <td
              class="px-3 py-3 font-mono text-[13px] max-md:border-b-0 max-md:p-0 max-md:text-xs"
              :class="[rowBorder(index), {'text-muted-foreground max-md:hidden': row.code === '–'}]"
            >
              {{ row.code }}
            </td>
            <td class="px-3 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]" :class="rowBorder(index)">{{ row.type }}</td>
            <td
              class="px-3 py-3 max-md:border-b-0 max-md:p-0"
              :class="[rowBorder(index), {'max-md:hidden': !row.status}]"
            >
              <CourseStatusBadge v-if="row.status" :status="row.status"/>
              <span v-else class="text-muted-foreground">–</span>
            </td>
            <td
              class="text-muted-foreground px-4 py-3 max-md:border-b-0 max-md:p-0 max-md:text-[13px]"
              :class="[rowBorder(index), {'max-md:hidden': row.period === '–'}]"
            >
              {{ row.period }}
            </td>
            <td v-if="isAdmin" class="w-12 px-3 py-1.5 text-right max-md:absolute max-md:top-2 max-md:right-2 max-md:w-auto max-md:border-b-0 max-md:p-0" :class="rowBorder(index)">
              <DropdownMenu v-if="row.group || row.course">
                <DropdownMenuTrigger as-child>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    :aria-label="`Åtgärder för ${row.name}`"
                    data-test="row-actions"
                  >
                    <PhDotsThree/>
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" class="min-w-40">
                  <DropdownMenuItem data-test="row-edit" @select="emit('edit', row)">
                    <PhPencilSimple/>
                    Redigera
                  </DropdownMenuItem>
                  <DropdownMenuItem variant="destructive" data-test="row-delete" @select="emit('delete', row)">
                    <PhTrash/>
                    Ta bort
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </td>
          </tr>
          <tr v-if="rows.length === 0">
            <td
              :colspan="isAdmin
                ? 6
                : 5"
              class="text-muted-foreground px-4 py-6 text-center"
            >
              {{ emptyText }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
