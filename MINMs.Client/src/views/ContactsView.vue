<script setup lang="ts">
import { Users } from 'lucide-vue-next'
</script>

<template>
  <div class="mx-auto max-w-3xl px-5 py-12 sm:py-16">
    <div
      class="overflow-hidden rounded-3xl border border-white/10 bg-gradient-to-b from-zinc-900/90 to-zinc-950/95 p-8 shadow-2xl shadow-black/40 backdrop-blur-xl"
    >
      <div class="flex size-14 items-center justify-center rounded-2xl bg-violet-500/15 text-violet-300 ring-1 ring-white/10">
        <Users class="size-7" aria-hidden="true" />
      </div>
      <h1 class="mt-6 text-2xl font-bold tracking-tight text-white">
        Контакты
      </h1>
    </div>
  </div>
</template>
