<script setup lang="ts">
import type {HTMLAttributes} from 'vue'

import {cn} from '@/lib/utils'

const props = defineProps<{class?: HTMLAttributes['class']}>()
</script>

<template>
  <!-- Lärkas märke: ett ä på blyertsgul platta, ena pricken i lärarens rödpenna. Samma färger i båda teman. -->
  <svg
    viewBox="0 0 64 64"
    role="img"
    aria-label="Lärka"
    :class="cn('size-8 shrink-0', props.class)"
  >
    <rect
      width="64"
      height="64"
      rx="14"
      fill="#f5c33b"
    />
    <g transform="translate(2.3 -2.6)">
      <circle
        cx="29"
        cy="40"
        r="11.5"
        fill="none"
        stroke="#1e3a32"
        stroke-width="7"
      />
      <rect
        x="37"
        y="25"
        width="7"
        height="30"
        fill="#1e3a32"
      />
      <circle
        cx="23"
        cy="15.5"
        r="4.2"
        fill="#1e3a32"
      />
      <circle
        cx="36"
        cy="15.5"
        r="4.2"
        fill="#d2483a"
      />
    </g>
  </svg>
</template>
