<template>
  <section class="flex flex-col items-start gap-3" data-test="not-found">
    <h1 class="text-3xl leading-tight">Sidan finns inte</h1>
    <p class="text-muted-foreground">Länken kan vara fel, eller så har sidan flyttats.</p>
    <RouterLink :to="{name: 'today'}">Till startsidan</RouterLink>
  </section>
</template>
