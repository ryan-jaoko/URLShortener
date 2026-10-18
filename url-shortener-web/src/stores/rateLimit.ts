import { defineStore } from "pinia";
import { computed, ref } from "vue";

interface BlockEntry {
  blockedUntil: number; // timestamp in ms
  remainingSeconds: number;
  message: string;
}

const STORAGE_PREFIX = "shortpath.block.";
const ATTEMPTS_PREFIX = "shortpath.attempts.";

export const useRateLimitStore = defineStore("rateLimit", () => {
  const blocks = ref<Record<string, BlockEntry>>({});
  const intervalId = ref<number | null>(null);

  function startTicker() {
    if (intervalId.value !== null) return;
    intervalId.value = window.setInterval(() => {
      const now = Date.now();
      let hasActiveBlocks = false;

      for (const [action, entry] of Object.entries(blocks.value)) {
        const remaining = Math.max(0, Math.ceil((entry.blockedUntil - now) / 1000));
        if (remaining <= 0) {
          delete blocks.value[action];
          localStorage.removeItem(`${STORAGE_PREFIX}${action}`);
        } else {
          entry.remainingSeconds = remaining;
          hasActiveBlocks = true;
        }
      }

      if (!hasActiveBlocks && intervalId.value !== null) {
        clearInterval(intervalId.value);
        intervalId.value = null;
      }
    }, 1000);
  }

  function restoreSavedBlocks() {
    const now = Date.now();
    for (let i = 0; i < localStorage.length; i++) {
      const key = localStorage.key(i);
      if (key && key.startsWith(STORAGE_PREFIX)) {
        const action = key.slice(STORAGE_PREFIX.length);
        try {
          const raw = localStorage.getItem(key);
          if (raw) {
            const data = JSON.parse(raw) as BlockEntry;
            const remaining = Math.max(0, Math.ceil((data.blockedUntil - now) / 1000));
            if (remaining > 0) {
              blocks.value[action] = {
                blockedUntil: data.blockedUntil,
                remainingSeconds: remaining,
                message: data.message || "Rate limit exceeded. Please wait.",
              };
            } else {
              localStorage.removeItem(key);
            }
          }
        } catch {
          localStorage.removeItem(key);
        }
      }
    }

    if (Object.keys(blocks.value).length > 0) {
      startTicker();
    }
  }

  // Restore any persistent blocks on initialization
  restoreSavedBlocks();

  function triggerBlock(action: string, seconds: number, message?: string) {
    const duration = Math.max(1, seconds);
    const blockedUntil = Date.now() + duration * 1000;
    const entry: BlockEntry = {
      blockedUntil,
      remainingSeconds: duration,
      message:
        message ||
        `Rate limit reached for ${action}. You are temporarily blocked. Please wait ${duration}s.`,
    };

    blocks.value = {
      ...blocks.value,
      [action]: entry,
    };

    try {
      localStorage.setItem(`${STORAGE_PREFIX}${action}`, JSON.stringify(entry));
    } catch {
      // Storage unavailable or full
    }

    startTicker();
  }

  function isBlocked(action: string): boolean {
    const entry = blocks.value[action];
    if (!entry) return false;
    return entry.remainingSeconds > 0;
  }

  function getRemainingSeconds(action: string): number {
    return blocks.value[action]?.remainingSeconds ?? 0;
  }

  function getBlockMessage(action: string): string {
    return blocks.value[action]?.message ?? "";
  }

  function clearBlock(action: string) {
    delete blocks.value[action];
    localStorage.removeItem(`${STORAGE_PREFIX}${action}`);
  }

  /**
   * Client-side proactive rate limiting / throttling.
   * Prevents spamming clicks before even sending to the backend.
   * Returns true if request is allowed, false if client blocked.
   */
  function recordAttempt(
    action: string,
    maxAttempts = 5,
    windowMs = 15000,
    blockSeconds = 30
  ): boolean {
    if (isBlocked(action)) {
      return false;
    }

    const now = Date.now();
    const storageKey = `${ATTEMPTS_PREFIX}${action}`;
    let timestamps: number[] = [];

    try {
      const raw = sessionStorage.getItem(storageKey);
      if (raw) {
        timestamps = JSON.parse(raw);
      }
    } catch {
      timestamps = [];
    }

    // Filter to within window
    timestamps = timestamps.filter((t) => now - t <= windowMs);

    if (timestamps.length >= maxAttempts) {
      triggerBlock(
        action,
        blockSeconds,
        `Too many consecutive attempts. Blocked for ${blockSeconds} seconds.`
      );
      sessionStorage.removeItem(storageKey);
      return false;
    }

    timestamps.push(now);
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(timestamps));
    } catch {
      // Ignore
    }

    return true;
  }

  const anyBlockActive = computed(() => Object.keys(blocks.value).length > 0);

  return {
    blocks,
    anyBlockActive,
    triggerBlock,
    isBlocked,
    getRemainingSeconds,
    getBlockMessage,
    clearBlock,
    recordAttempt,
  };
});
