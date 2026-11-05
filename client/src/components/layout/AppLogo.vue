<script setup lang="ts">
import type {HTMLAttributes} from 'vue'

import {cn} from '@/lib/utils'

const props = defineProps<{class?: HTMLAttributes['class']}>()
</script>

<template>
  <!-- Lärkas märke: ett kritvitt ä med volt linje under. Det står utan platta, eftersom ytorna i appen där det visas redan är tavlan. Samma färger i båda teman. -->
  <svg
    viewBox="7 7 50 50"
    role="img"
    aria-label="Lärka"
    :class="cn('size-8 shrink-0', props.class)"
  >
    <g transform="translate(32 31.6) scale(.9) translate(-29 -38.7)">
      <circle
        cx="29"
        cy="40"
        r="11.5"
        fill="none"
        stroke="#edf2e9"
        stroke-width="7"
      />
      <rect
        x="37"
        y="25"
        width="7"
        height="30"
        fill="#edf2e9"
      />
      <circle
        cx="23"
        cy="17.6"
        r="4.2"
        fill="#edf2e9"
      />
      <circle
        cx="36"
        cy="17.6"
        r="4.2"
        fill="#edf2e9"
      />
      <rect
        x="14"
        y="59"
        width="30"
        height="5"
        rx="1"
        fill="#dfff00"
      />
    </g>
  </svg>
</template>
