<script setup lang="ts">
import {useRoute} from 'vue-router'

// nested: sidan ligger under en annan sidas rubrik (t.ex. en flik på kurssidan) och får en mindre rubrik.
defineProps<{nested?: boolean}>()

const route = useRoute()
</script>

<template>
  <section class="flex flex-col gap-1" data-test="placeholder">
    <h2 v-if="nested" class="text-base">{{ route.meta.title }}</h2>
    <h1 v-else class="text-3xl leading-tight">{{ route.meta.title }}</h1>
    <p class="text-muted-foreground">Den här sidan är inte byggd än.</p>
  </section>
</template>
