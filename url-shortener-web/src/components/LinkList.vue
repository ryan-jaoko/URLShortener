<script setup lang="ts">
import { useAuthStore } from "../stores/auth";
import { useLinksStore } from "../stores/links";

const auth = useAuthStore();
const links = useLinksStore();

function hostOf(url: string) {
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
}

async function onDelete(shortCode: string) {
  if (!window.confirm(`Delete ${shortCode}? This cannot be undone.`)) return;
  try {
    await links.remove(shortCode);
  } catch {
    // Error is shown from the store.
  }
}
</script>

<template>
  <section class="rounded-3xl border border-line bg-card p-5 sm:p-6">
    <div class="flex items-end justify-between gap-3">
      <div>
        <h3 class="font-display text-2xl">Links</h3>
        <p class="text-sm text-muted">
          Your saved short links.
        </p>
      </div>
      <p class="text-sm text-muted">{{ links.links.length }} saved</p>
    </div>

    <ul v-if="links.links.length" class="mt-4 divide-y divide-line">
      <li
        v-for="link in links.links"
        :key="link.shortCode"
        class="flex flex-col gap-2 py-3 sm:flex-row sm:items-center"
      >
        <button
          type="button"
          class="min-w-0 flex-1 text-left"
          @click="links.loadStats(link.shortCode)"
        >
          <p class="truncate font-medium text-pine">/{{ link.shortCode }}</p>
          <p class="truncate text-sm text-muted">{{ hostOf(link.originalUrl) }}</p>
        </button>
        <div class="flex items-center gap-3 text-sm">
          <span class="rounded-full bg-paper px-2 py-1 text-muted">
            {{ link.clickCount }} clicks
          </span>
          <button
            type="button"
            class="text-ember-dark hover:underline"
            @click="onDelete(link.shortCode)"
          >
            Delete
          </button>
        </div>
      </li>
    </ul>

    <p v-else class="mt-6 rounded-2xl bg-paper px-4 py-8 text-center text-muted">
      No links yet. Shorten one to see it here.
    </p>
  </section>
</template>
