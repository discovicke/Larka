<script setup lang="ts">
import GitHubIntegrationSection from '@/components/settings/GitHubIntegrationSection.vue'
import MicrosoftIntegration from '@/components/settings/MicrosoftIntegration.vue'
</script>

<template>
  <section
    id="integrationer"
    class="scroll-mt-6 flex flex-col gap-3"
    data-test="integrations"
  >
    <header class="flex flex-col gap-1">
      <h2 class="font-heading text-xl font-semibold">Integrationer</h2>
      <p class="text-muted-foreground text-sm">
        Anslutningar som gäller hela skolan. Bara admin kan se dem.
      </p>
    </header>

    <MicrosoftIntegration/>

    <GitHubIntegrationSection/>
  </section>
</template>
