<script setup lang="ts">
import {computed} from 'vue'

import {Button} from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'

import type {FormResult} from './types'

const props = defineProps<{
  title: string
  description?: string
  result: FormResult | null
  submitLabel?: string
  busy?: boolean
}>()

const emit = defineEmits<{submit: []}>()

const formattedBody = computed(() => {
  if (!props.result || props.result.kind === 'error') {
    return ''
  }
  return JSON.stringify(props.result.body, null, 2)
})
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle>{{ title }}</CardTitle>
      <CardDescription v-if="description">{{ description }}</CardDescription>
    </CardHeader>

    <form class="flex flex-col gap-4" @submit.prevent="emit('submit')">
      <CardContent class="flex flex-col gap-3">
        <slot/>
      </CardContent>

      <CardContent v-if="result" data-test="result">
        <p v-if="result.kind === 'response'" class="mb-1 font-medium">
          {{ result.status }}
        </p>
        <p v-else class="text-destructive font-medium">
          <template v-if="result.status">{{ result.status }}:</template>
          {{ result.message }}
        </p>
        <pre
          v-if="result.kind !== 'error'"
          class="bg-muted overflow-x-auto rounded-md p-3 text-xs"
        >{{ formattedBody }}</pre>
      </CardContent>

      <CardFooter>
        <Button type="submit" :disabled="busy">
          {{ submitLabel ?? 'Create' }}
        </Button>
      </CardFooter>
    </form>
  </Card>
</template>
