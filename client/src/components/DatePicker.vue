<script setup lang="ts">
import {computed, ref} from 'vue'
import {PhCalendarBlank} from '@phosphor-icons/vue'
import {Button} from '@/components/ui/button'
import {Calendar} from '@/components/ui/calendar'
import {Popover, PopoverContent, PopoverTrigger} from '@/components/ui/popover'
import {formatCalendarDate, fromCalendarDate, toCalendarDate} from '@/lib/dateValue'
import {cn} from 'cn'

defineOptions({inheritAttrs: false})

// v-model: 'yyyy-mm-dd', tomt = inget datum.
const props = withDefaults(
  defineProps<{
    modelValue?: string
    id?: string
    placeholder?: string
    disabled?: boolean
    min?: string
    max?: string
    clearable?: boolean
    invalid?: boolean
  }>(),
  {modelValue: '', id: undefined, min: undefined, max: undefined, placeholder: 'Välj datum'},
)
const emit = defineEmits<{'update:modelValue': [value: string]}>()

const open = ref(false)
const selected = computed(() => toCalendarDate(props.modelValue))
const label = computed(() => formatCalendarDate(props.modelValue))

function onSelect(value: unknown) {
  if (!value) return
  emit('update:modelValue', fromCalendarDate(value as {year: number, month: number, day: number}))
  open.value = false
}

function clear() {
  emit('update:modelValue', '')
  open.value = false
}
</script>

<template>
  <Popover v-model:open="open">
    <PopoverTrigger as-child>
      <Button
        :id="id"
        type="button"
        variant="outline"
        :disabled="disabled"
        :aria-invalid="invalid || undefined"
        v-bind="$attrs"
        :class="cn('w-full justify-start px-2.5 font-normal', !label && 'text-muted-foreground')"
      >
        <PhCalendarBlank data-icon="inline-start" class="size-4"/>
        <span class="truncate">{{ label || placeholder }}</span>
      </Button>
    </PopoverTrigger>
    <PopoverContent class="w-auto p-0" align="start">
      <Calendar
        :model-value="selected"
        locale="sv-SE"
        calendar-label="Datum"
        :week-starts-on="1"
        :min-value="toCalendarDate(min)"
        :max-value="toCalendarDate(max)"
        :default-placeholder="toCalendarDate(min) ?? undefined"
        layout="month-and-year"
        fixed-weeks
        initial-focus
        @update:model-value="onSelect"
      />
      <div v-if="clearable && modelValue" class="border-t p-2">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          @click="clear"
        >
          Rensa
        </Button>
      </div>
    </PopoverContent>
  </Popover>
</template>
