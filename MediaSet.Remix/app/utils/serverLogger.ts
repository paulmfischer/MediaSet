/**
 * Server-side logger utility for Remix loaders/actions.
 *
 * This module provides a logging interface that forwards logs to the API's
 * POST /api/logs endpoint, allowing server-side logs to be aggregated with
 * client-side logs and enriched with Application and Environment context.
 *
 * Uses apiFetch() to automatically include W3C traceparent header for
 * distributed tracing across the Remix server and API.
 */

import { apiFetch } from "./apiFetch.server";

interface ServerLogPayload {
  level: "Debug" | "Information" | "Warning" | "Error";
  message: string;
  timestamp: string;
  properties?: Record<string, unknown>;
}

const API_BASE_URL = process.env.apiUrl || "http://localhost:7130";

/**
 * Sends a log event to the API.
 * Non-blocking; errors are logged to console but don't throw.
 * Uses apiFetch() which automatically includes the W3C traceparent header
 * from the request context for distributed tracing.
 */
async function sendLogToApi(payload: ServerLogPayload): Promise<void> {
  try {
    const response = await apiFetch(`${API_BASE_URL}/api/logs`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      console.warn(`[ServerLogger] API returned ${response.status} when logging`);
    }
  } catch (error) {
    // Silently fail on API errors to avoid infinite loops or disruptions
    console.warn("[ServerLogger] Failed to send log to API:", error);
  }
}

/**
 * Server logger API for Remix loaders/actions.
 */
export const serverLogger = {
  /**
   * Log an informational message.
   */
  info(message: string, properties?: Record<string, unknown>): void {
    console.log(`[INFO] ${message}`, properties || "");

    const payload: ServerLogPayload = {
      level: "Information",
      message,
      timestamp: new Date().toISOString(),
      properties,
    };
    sendLogToApi(payload);
  },

  /**
   * Log a warning message.
   */
  warn(message: string, properties?: Record<string, unknown>): void {
    console.warn(`[WARN] ${message}`, properties || "");

    const payload: ServerLogPayload = {
      level: "Warning",
      message,
      timestamp: new Date().toISOString(),
      properties,
    };
    sendLogToApi(payload);
  },

  /**
   * Log an error message.
   */
  error(message: string, error?: Error | string | unknown, properties?: Record<string, unknown>): void {
    let fullMessage = message;

    if (error) {
      const errorStr = error instanceof Error ? `${error.name}: ${error.message}` : String(error);
      fullMessage = `${message}: ${errorStr}`;
    }

    console.error(`[ERROR] ${fullMessage}`, properties || "");

    const payload: ServerLogPayload = {
      level: "Error",
      message: fullMessage,
      timestamp: new Date().toISOString(),
      properties,
    };
    sendLogToApi(payload);
  },

  /**
   * Log a debug message.
   */
  debug(message: string, properties?: Record<string, unknown>): void {
    console.debug(`[DEBUG] ${message}`, properties || "");

    const payload: ServerLogPayload = {
      level: "Debug",
      message,
      timestamp: new Date().toISOString(),
      properties,
    };
    sendLogToApi(payload);
  },
};
