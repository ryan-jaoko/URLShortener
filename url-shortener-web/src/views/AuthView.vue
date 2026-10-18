<script setup lang="ts">
import { computed, ref } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import { useAuthStore } from "../stores/auth";
import { useLinksStore } from "../stores/links";
import { useRateLimitStore } from "../stores/rateLimit";

const auth = useAuthStore();
const links = useLinksStore();
const rateLimit = useRateLimitStore();
const route = useRoute();
const router = useRouter();

const mode = computed(() => (route.meta.mode === "register" ? "register" : "login"));
const username = ref("");
const email = ref("");
const password = ref("");
const localError = ref("");

const isBlocked = computed(() => rateLimit.isBlocked(mode.value));
const remainingSeconds = computed(() => rateLimit.getRemainingSeconds(mode.value));
const hasPending = computed(() => Boolean(links.pendingShorten?.longUrl));

async function onSubmit() {
  localError.value = "";

  if (isBlocked.value) {
    localError.value = `Rate limit block active. Please wait ${remainingSeconds.value} seconds.`;
    return;
  }

  if (!email.value.trim() || !password.value) {
    localError.value = "Email and password are required.";
    return;
  }
  if (mode.value === "register" && !username.value.trim()) {
    localError.value = "Pick a username.";
    return;
  }

  // Client-side rate limiting / block check: 5 attempts per 30s, block for 60s
  if (!rateLimit.recordAttempt(mode.value, 5, 30000, 60)) {
    localError.value = `Too many attempts. You are temporarily blocked for ${rateLimit.getRemainingSeconds(mode.value)}s.`;
    return;
  }

  try {
    if (mode.value === "register") {
      await auth.register({
        username: username.value.trim(),
        email: email.value.trim(),
        password: password.value,
      });
    } else {
      await auth.login({
        email: email.value.trim(),
        password: password.value,
      });
    }

    // Automatically shorten the remembered link immediately after authentication
    if (links.pendingShorten) {
      const pending = { ...links.pendingShorten };
      try {
        await links.shorten(pending);
      } catch {
        // Store holds the error
      } finally {
        links.clearPendingShorten();
      }
    }

    await router.push("/");
  } catch {
    // Store holds the API error.
  }
}
</script>

<template>
  <div class="mx-auto max-w-md">
    <section class="rounded-3xl border border-line bg-card p-7 shadow-[0_24px_50px_-32px_rgba(28,25,20,0.45)]">
      <p class="text-xs uppercase tracking-[0.22em] text-ember">
        {{ mode === "register" ? "Create account" : "Welcome back" }}
      </p>
      <h1 class="mt-2 font-display text-4xl">
        {{ mode === "register" ? "Sign up" : "Log in" }}
      </h1>
      <p class="mt-2 text-muted">
        JWT authentication with secure token storage and session management.
      </p>

      <!-- Prompt for pending link to shorten -->
      <div
        v-if="hasPending"
        class="mt-5 rounded-2xl border border-pine/30 bg-pine/10 p-4 text-sm text-ink"
      >
        <p class="font-medium text-pine">Almost there! Complete {{ mode === 'register' ? 'registration' : 'login' }} to shorten:</p>
        <p class="mt-1 font-mono text-xs text-muted truncate">{{ links.pendingShorten?.longUrl }}</p>
      </div>

      <!-- Rate limit block warning banner with live countdown -->
      <div
        v-if="isBlocked"
        class="mt-5 flex items-center gap-3 rounded-2xl border border-ember/40 bg-ember/10 p-4 text-sm text-ember-dark"
      >
        <span class="text-2xl" aria-hidden="true">⏳</span>
        <div class="flex-1">
          <p class="font-semibold">Too many attempts (Blocked)</p>
          <p class="mt-0.5 text-xs text-ink/80">
            For security, further attempts are temporarily paused. Try again in
            <span class="font-bold text-ember">{{ remainingSeconds }}s</span>.
          </p>
        </div>
      </div>

      <form class="mt-6 space-y-4" @submit.prevent="onSubmit">
        <label v-if="mode === 'register'" class="block">
          <span class="mb-1.5 block text-sm text-muted">Username</span>
          <input
            v-model="username"
            type="text"
            autocomplete="username"
            :disabled="isBlocked"
            class="w-full rounded-2xl border border-line bg-paper px-4 py-3 outline-none ring-ember/30 focus:ring-2 disabled:opacity-60"
          />
        </label>
        <label class="block">
          <span class="mb-1.5 block text-sm text-muted">Email</span>
          <input
            v-model="email"
            type="email"
            autocomplete="email"
            :disabled="isBlocked"
            class="w-full rounded-2xl border border-line bg-paper px-4 py-3 outline-none ring-ember/30 focus:ring-2 disabled:opacity-60"
          />
        </label>
        <label class="block">
          <span class="mb-1.5 block text-sm text-muted">Password</span>
          <input
            v-model="password"
            type="password"
            :disabled="isBlocked"
            :autocomplete="mode === 'register' ? 'new-password' : 'current-password'"
            class="w-full rounded-2xl border border-line bg-paper px-4 py-3 outline-none ring-ember/30 focus:ring-2 disabled:opacity-60"
          />
        </label>

        <p v-if="localError || auth.error" class="text-sm text-ember-dark">
          {{ localError || auth.error }}
        </p>

        <button
          type="submit"
          class="w-full rounded-full bg-pine py-3 text-card hover:bg-moss disabled:opacity-60 transition"
          :disabled="auth.loading || isBlocked"
        >
          <template v-if="isBlocked">
            Temporarily blocked ({{ remainingSeconds }}s)
          </template>
          <template v-else-if="auth.loading">
            Working…
          </template>
          <template v-else>
            {{ mode === "register" ? "Create account" : "Log in" }}
          </template>
        </button>
      </form>

      <p class="mt-5 text-center text-sm text-muted">
        <template v-if="mode === 'register'">
          Already have an account?
          <RouterLink to="/login" class="text-pine underline underline-offset-4">Log in</RouterLink>
        </template>
        <template v-else>
          New here?
          <RouterLink to="/register" class="text-pine underline underline-offset-4">Sign up</RouterLink>
        </template>
      </p>
    </section>
  </div>
</template>
