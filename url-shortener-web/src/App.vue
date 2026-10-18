<script setup lang="ts">
import AppHeader from "./components/AppHeader.vue";
import { useRateLimitStore } from "./stores/rateLimit";

const rateLimit = useRateLimitStore();
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <div
      v-if="rateLimit.isBlocked('general')"
      class="bg-ember px-4 py-2 text-center text-sm font-medium text-card flex items-center justify-center gap-2 shadow-sm"
    >
      <span aria-hidden="true">⚠️</span>
      <span>
        Rate limit active: API requests are temporarily paused. Unblocking in
        <strong>{{ rateLimit.getRemainingSeconds('general') }}s</strong>.
      </span>
    </div>
    <AppHeader />
    <main class="mx-auto w-full max-w-6xl flex-1 px-5 py-8 sm:py-12">
      <RouterView />
    </main>
    <footer class="border-t border-line/70 px-5 py-6 text-center text-sm text-muted">
      Shortpath · URL Shortener with JWT Authentication & Rate Limiting
    </footer>
  </div>
</template>
