<script setup lang="ts">
import { ArrowRight, MessagesSquare, Shield, Users } from 'lucide-vue-next'
</script>

<template>
  <div class="relative min-h-[calc(100dvh-5.25rem)] overflow-hidden">
    <div
      class="minms-home-gradient absolute inset-0"
      aria-hidden="true"
    />
    <div class="minms-noise absolute inset-0" aria-hidden="true" />

    <div class="relative z-10 mx-auto flex max-w-5xl flex-col gap-12 px-5 py-14 sm:py-20">
      <section class="max-w-2xl">
        <p
          class="mb-4 inline-flex rounded-full border border-white/20 bg-black/20 px-3 py-1 text-xs font-medium uppercase tracking-widest text-emerald-100/90 backdrop-blur-sm"
        >
          Мессенджер и контакты
        </p>
        <h1 class="text-4xl font-bold leading-tight tracking-tight text-white text-balance drop-shadow-sm sm:text-5xl">
          Общайтесь спокойно в одном месте
        </h1>
        <p class="mt-5 max-w-xl text-lg leading-relaxed text-white/85">
          <strong class="font-semibold text-white">MINMs</strong> — это веб-приложение для личных и групповых чатов:
          пользователи, контакты, блокировки, участники чатов и сообщения хранятся в надёжной базе данных, а интерфейс
          построен на Vue и современном стеке .NET.
        </p>
        <div class="mt-8 flex flex-wrap gap-3">
          <RouterLink
            to="/users"
            class="inline-flex items-center gap-2 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-teal-900 shadow-lg shadow-black/25 transition hover:bg-emerald-50"
          >
            Найти людей
            <ArrowRight class="size-4" aria-hidden="true" />
          </RouterLink>
        </div>
      </section>

      <section class="grid gap-4 sm:grid-cols-3">
        <article
          class="rounded-2xl border border-white/15 bg-black/25 p-5 shadow-xl backdrop-blur-md transition hover:border-white/25 hover:bg-black/30"
        >
          <div class="mb-3 flex size-11 items-center justify-center rounded-xl bg-emerald-500/20 text-emerald-200">
            <MessagesSquare class="size-5" aria-hidden="true" />
          </div>
          <h2 class="text-base font-semibold text-white">
            Чаты и сообщения
          </h2>
          <p class="mt-2 text-sm leading-relaxed text-white/75">
            Модель данных включает чаты, участников и историю сообщений — основа для полноценного диалога.
          </p>
        </article>
        <article
          class="rounded-2xl border border-white/15 bg-black/25 p-5 shadow-xl backdrop-blur-md transition hover:border-white/25 hover:bg-black/30"
        >
          <div class="mb-3 flex size-11 items-center justify-center rounded-xl bg-violet-500/20 text-violet-200">
            <Users class="size-5" aria-hidden="true" />
          </div>
          <h2 class="text-base font-semibold text-white">
            Люди и контакты
          </h2>
          <p class="mt-2 text-sm leading-relaxed text-white/75">
            Учётные записи, списки контактов и поиск по имени помогают находить собеседников.
          </p>
        </article>
        <article
          class="rounded-2xl border border-white/15 bg-black/25 p-5 shadow-xl backdrop-blur-md transition hover:border-white/25 hover:bg-black/30"
        >
          <div class="mb-3 flex size-11 items-center justify-center rounded-xl bg-rose-500/20 text-rose-200">
            <Shield class="size-5" aria-hidden="true" />
          </div>
          <h2 class="text-base font-semibold text-white">
            Контроль и приватность
          </h2>
          <p class="mt-2 text-sm leading-relaxed text-white/75">
            В схеме БД предусмотрены блокировки и статусы участников — можно развивать модерацию и безопасность.
          </p>
        </article>
      </section>
    </div>
  </div>
</template>
