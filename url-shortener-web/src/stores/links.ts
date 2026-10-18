import { defineStore } from "pinia";
import { computed, ref } from "vue";
import { getErrorMessage, PUBLIC_SHORT_BASE_URL } from "../api/client";
import * as urlsApi from "../api/urls";
import type { LinkStats, ShortenRequest, ShortLink } from "../api/types";

const PENDING_KEY = "shortpath.pending_shorten";

function readPending(): ShortenRequest | null {
  try {
    const raw = sessionStorage.getItem(PENDING_KEY);
    return raw ? (JSON.parse(raw) as ShortenRequest) : null;
  } catch {
    return null;
  }
}

export const useLinksStore = defineStore("links", () => {
  const links = ref<ShortLink[]>([]);
  const lastCreated = ref<ShortLink | null>(null);
  const selectedStats = ref<LinkStats | null>(null);
  const selectedCode = ref("");
  const loading = ref(false);
  const statsLoading = ref(false);
  const error = ref("");
  const pendingShorten = ref<ShortenRequest | null>(readPending());

  function setPendingShorten(payload: ShortenRequest) {
    pendingShorten.value = payload;
    try {
      sessionStorage.setItem(PENDING_KEY, JSON.stringify(payload));
    } catch {
      // Ignore
    }
  }

  function clearPendingShorten() {
    pendingShorten.value = null;
    try {
      sessionStorage.removeItem(PENDING_KEY);
    } catch {
      // Ignore
    }
  }

  const totalClicks = computed(() =>
    links.value.reduce((sum, link) => sum + (link.clickCount ?? 0), 0),
  );

  async function shorten(payload: ShortenRequest) {
    loading.value = true;
    error.value = "";
    try {
      const created = await urlsApi.shortenUrl(payload);
      if (!created.shortUrl) {
        created.shortUrl = `${PUBLIC_SHORT_BASE_URL.replace(/\/$/, "")}/${created.shortCode}`;
      }
      lastCreated.value = created;
      if (pendingShorten.value?.longUrl === payload.longUrl) {
        clearPendingShorten();
      }
      // Refresh user's links directly from the database
      await loadMine();
      return created;
    } catch (err) {
      error.value = getErrorMessage(err);
      throw err;
    } finally {
      loading.value = false;
    }
  }

  async function loadMine() {
    loading.value = true;
    error.value = "";
    try {
      const mine = await urlsApi.listMyUrls();
      links.value = mine;
    } catch (err) {
      error.value = getErrorMessage(err);
    } finally {
      loading.value = false;
    }
  }

  async function loadStats(shortCode: string) {
    statsLoading.value = true;
    error.value = "";
    selectedCode.value = shortCode;
    try {
      selectedStats.value = await urlsApi.getUrlStats(shortCode);
    } catch (err) {
      selectedStats.value = null;
      error.value = getErrorMessage(err);
    } finally {
      statsLoading.value = false;
    }
  }

  async function remove(shortCode: string) {
    error.value = "";
    try {
      await urlsApi.deleteUrl(shortCode);
      links.value = links.value.filter((link) => link.shortCode !== shortCode);
      if (selectedCode.value === shortCode) {
        selectedStats.value = null;
        selectedCode.value = "";
      }
    } catch (err) {
      error.value = getErrorMessage(err);
      throw err;
    }
  }

  return {
    links,
    lastCreated,
    selectedStats,
    selectedCode,
    loading,
    statsLoading,
    error,
    pendingShorten,
    totalClicks,
    setPendingShorten,
    clearPendingShorten,
    shorten,
    loadMine,
    loadStats,
    remove,
  };
});
