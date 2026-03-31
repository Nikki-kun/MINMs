<script setup lang="ts">
import { MessageCircle } from 'lucide-vue-next'
</script>

<template>
  <div class="mx-auto max-w-3xl px-6 py-8">
    <header class="mb-6 flex items-center justify-between gap-4 border-b border-zinc-200 pb-4 dark:border-zinc-800">
      <div class="flex items-center gap-2 text-zinc-900 dark:text-zinc-100">
        <MessageCircle class="size-7 shrink-0 text-emerald-600 dark:text-emerald-400" aria-hidden="true" />
        <span class="text-lg font-semibold tracking-tight">MINMs</span>
      </div>
      <nav class="flex gap-4 text-sm font-medium text-zinc-600 dark:text-zinc-400">
        <RouterLink
          to="/"
          class="rounded-md px-2 py-1 transition-colors hover:bg-zinc-100 hover:text-zinc-900 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
          active-class="text-emerald-700 dark:text-emerald-400"
        >
          Home
        </RouterLink>
        <RouterLink
          to="/about"
          class="rounded-md px-2 py-1 transition-colors hover:bg-zinc-100 hover:text-zinc-900 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
          active-class="text-emerald-700 dark:text-emerald-400"
        >
          About
        </RouterLink>
      </nav>
    </header>

    <main class="pt-1">
      <RouterView />
    </main>
  </div>
</template>
