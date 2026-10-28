<script setup lang="ts">
import {computed, ref} from 'vue'
import {PhCalendarBlank} from '@phosphor-icons/vue'
import {Button} from '@/components/ui/button'
import {Calendar} from '@/components/ui/calendar'
import {Input} from '@/components/ui/input'
import {Popover, PopoverContent, PopoverTrigger} from '@/components/ui/popover'
import {formatDateTime, fromCalendarDate, splitDateTime, toCalendarDate} from '@/lib/dateValue'
import {cn} from 'cn'

defineOptions({inheritAttrs: false})

// v-model: 'yyyy-mm-ddThh:mm' (lokal tid), tomt = inget värde.
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
  {modelValue: '', id: undefined, min: undefined, max: undefined, placeholder: 'Välj datum och tid'},
)
const emit = defineEmits<{'update:modelValue': [value: string]}>()

const DEFAULT_TIME = '09:00'
const open = ref(false)
const parts = computed(() => splitDateTime(props.modelValue))
const selected = computed(() => toCalendarDate(parts.value.date))
const label = computed(() => formatDateTime(props.modelValue))

function onSelect(value: unknown) {
  if (!value) return
  const date = fromCalendarDate(value as {year: number, month: number, day: number})
  emit('update:modelValue', `${date}T${parts.value.time || DEFAULT_TIME}`)
}

function onTime(value: string | number) {
  const time = String(value)
  if (!time) return
  const date = parts.value.date || fromCalendarDate(toCalendarDate(new Date().toLocaleDateString('sv-SE'))!)
  emit('update:modelValue', `${date}T${time}`)
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
        :week-starts-on="1"
        :min-value="toCalendarDate(min)"
        :max-value="toCalendarDate(max)"
        fixed-weeks
        initial-focus
        @update:model-value="onSelect"
      />
      <div class="flex items-center gap-2 border-t p-2">
        <label
          :for="id
            ? `${id}-time`
            : undefined"
          class="text-sm text-muted-foreground"
        >Tid</label>
        <Input
          :id="id
            ? `${id}-time`
            : undefined"
          type="time"
          step="60"
          class="w-auto"
          :model-value="parts.time"
          @update:model-value="onTime"
        />
        <Button
          v-if="clearable && modelValue"
          type="button"
          variant="ghost"
          size="sm"
          class="ml-auto"
          @click="clear"
        >
          Rensa
        </Button>
      </div>
    </PopoverContent>
  </Popover>
</template>
