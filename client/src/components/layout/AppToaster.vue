<script setup lang="ts">
import {PhCheckCircle, PhX} from '@phosphor-icons/vue'

import {Button} from '@/components/ui/button'
import {useToast} from '@/composables/useToast'

// Regionen ligger alltid i sidan, så att skärmläsaren hör nya toasts. Under md ligger den ovanför
// sidans nederkant med marginal, och på desktop nere till höger.
const {toasts, dismissToast} = useToast()
</script>

<template>
  <div
    aria-live="polite"
    role="status"
    data-test="toaster"
    class="pointer-events-none fixed inset-x-4 bottom-4 z-50 flex flex-col items-center gap-2 md:inset-x-auto md:right-6 md:bottom-6 md:items-end"
  >
    <div
      v-for="toast in toasts"
      :key="toast.id"
      data-test="toast"
      class="bg-popover text-popover-foreground pointer-events-auto flex w-full max-w-sm items-center gap-2 rounded-lg border py-2 pr-1.5 pl-3 text-sm shadow-lg"
    >
      <PhCheckCircle class="text-success size-4 shrink-0" weight="fill" aria-hidden="true"/>
      <span class="min-w-0 flex-1">{{ toast.message }}</span>
      <Button
        variant="ghost"
        size="icon-sm"
        aria-label="Stäng"
        @click="dismissToast(toast.id)"
      >
        <PhX aria-hidden="true"/>
      </Button>
    </div>
  </div>
</template>
