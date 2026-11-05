<script setup lang="ts">
import {computed} from 'vue'

import {mentionParts} from '@/lib/mentions'

const props = defineProps<{
  bodyMd: string
}>()

// En omnämning visas som ett markerat namn, inte som texten @[Namn](id) den sparas som.
const parts = computed(() => mentionParts(props.bodyMd))
</script>

<template>
  <p class="text-foreground/80 whitespace-pre-line wrap-anywhere">
    <template v-for="(part, index) in parts" :key="index">
      <span
        v-if="'userId' in part"
        class="text-primary font-medium"
        data-test="comment-mention"
      >@{{ part.name }}</span>
      <template v-else>{{ part.text }}</template>
    </template>
  </p>
</template>
