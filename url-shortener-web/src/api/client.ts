import axios, { AxiosError, type AxiosInstance } from "axios";
import { useRateLimitStore } from "../stores/rateLimit";
import type { ApiErrorBody } from "./types";

export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5062";

export const PUBLIC_SHORT_BASE_URL =
  import.meta.env.VITE_PUBLIC_SHORT_BASE_URL ?? API_BASE_URL;

const TOKEN_KEY = "shortpath.token";

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setStoredToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token);
  } else {
    localStorage.removeItem(TOKEN_KEY);
  }
}

export const apiClient: AxiosInstance = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    "Content-Type": "application/json",
  },
  timeout: 15_000,
});

apiClient.interceptors.request.use((config) => {
  const token = getStoredToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error)) {
      const status = error.response?.status;
      const url = error.config?.url ?? "";

      // Handle Rate Limiting (429)
      if (status === 429) {
        let action = "general";
        if (url.includes("/api/auth/login")) {
          action = "login";
        } else if (url.includes("/api/auth/register")) {
          action = "register";
        } else if (url.includes("/api/url/shorten")) {
          action = "shorten";
        }

        const data = error.response?.data as { retryAfter?: number; message?: string } | undefined;
        const retryHeader = error.response?.headers?.["retry-after"];
        const retryAfterSeconds =
          typeof data?.retryAfter === "number"
            ? data.retryAfter
            : retryHeader
            ? parseInt(String(retryHeader), 10)
            : 60;

        const message =
          data?.message ||
          `Too many requests. You have been temporarily blocked for ${retryAfterSeconds} seconds.`;

        try {
          const rateLimitStore = useRateLimitStore();
          rateLimitStore.triggerBlock(action, retryAfterSeconds, message);
        } catch {
          // In case store not ready
        }
      }

      // Handle Unauthorized (401) on authenticated requests
      if (status === 401 && !url.includes("/api/auth/login")) {
        setStoredToken(null);
      }
    }
    return Promise.reject(error);
  }
);

export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ApiErrorBody | string>;
    const data = axiosError.response?.data;

    if (typeof data === "string" && data.trim()) {
      return data;
    }

    if (data && typeof data === "object") {
      if (data.message) return data.message;
      if (data.title) return data.title;
      if (data.errors) {
        const first = Object.values(data.errors).flat()[0];
        if (first) return first;
      }
    }

    if (axiosError.code === "ERR_NETWORK") {
      return `Cannot reach the API at ${API_BASE_URL}. Is the backend running?`;
    }

    if (axiosError.response?.status === 429) {
      return "Rate limit exceeded. You are temporarily blocked. Please wait.";
    }

    if (axiosError.response?.status) {
      return `Request failed (${axiosError.response.status}).`;
    }
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Something went wrong.";
}
