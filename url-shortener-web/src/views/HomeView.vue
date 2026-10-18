<script setup lang="ts">
import { onMounted } from "vue";
import LinkList from "../components/LinkList.vue";
import ShortenForm from "../components/ShortenForm.vue";
import StatsPanel from "../components/StatsPanel.vue";
import { useAuthStore } from "../stores/auth";
import { useLinksStore } from "../stores/links";

const auth = useAuthStore();
const links = useLinksStore();

onMounted(() => {
  if (auth.isAuthenticated) {
    void links.loadMine();
  }
});
</script>

<template>
  <div class="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
    <div class="space-y-6">
      <ShortenForm />
      <StatsPanel />
    </div>
    <aside class="space-y-4">
      <div class="rounded-3xl border border-line bg-pine p-5 text-card">
        <p class="text-xs uppercase tracking-[0.2em] text-card/70">Overview</p>
        <p class="mt-3 font-display text-4xl">{{ links.totalClicks }}</p>
        <p class="mt-1 text-sm text-card/80">clicks across your links</p>
      </div>
      <LinkList />
    </aside>
  </div>
</template>
