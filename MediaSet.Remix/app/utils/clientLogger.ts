/**
 * Client-side logger utility that forwards logs to the API.
 *
 * This module provides a simple logging interface that:
 * - Wraps console methods (log, warn, error)
 * - Always forwards logs to POST /api/logs
 * - Includes minimal browser metadata
 * - Scrubs sensitive values before sending
 *
 * The API is responsible for enrichment (Application: MediaSet.Remix, Environment),
 * routing to external loggers (Seq), and retention policies.
 */

interface LogPayload {
  level: "Debug" | "Information" | "Warning" | "Error";
  message: string;
  timestamp: string;
  properties?: Record<string, unknown>;
}

/**
 * Sends a log event to the API.
 * Non-blocking; errors are logged to console but don't throw.
 */
async function sendLogToApi(payload: LogPayload): Promise<void> {
  try {
    await fetch("/api/logs", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
  } catch (error) {
    // Silently fail on API errors to avoid infinite loops or disruptions
    // Only log to console if it's a critical fetch error
    if (error instanceof TypeError && error.message.includes("Failed to fetch")) {
      console.warn("[ClientLogger] Failed to send log to API:", error);
    }
  }
}

/**
 * Sanitize log messages and properties by removing sensitive patterns.
 */
function sanitizeForLogging(value: unknown): unknown {
  if (typeof value === "string") {
    // Remove common sensitive patterns
    return value
      .replace(/Bearer\s+[^\s]+/gi, "Bearer [REDACTED]")
      .replace(/api[_-]?key[=:]\s*[^\s,;]+/gi, "api_key=[REDACTED]")
      .replace(/password[=:]\s*[^\s,;]+/gi, "password=[REDACTED]");
  }
  if (value && typeof value === "object") {
    const sanitized: Record<string, unknown> = {};
    for (const [key, val] of Object.entries(value)) {
      sanitized[key] = sanitizeForLogging(val);
    }
    return sanitized;
  }
  return value;
}

/**
 * Format an Error object or string for logging.
 */
function formatError(error: Error | string | unknown): string {
  if (error instanceof Error) {
    return `${error.name}: ${error.message}`;
  }
  return String(error);
}

/**
 * Client logger API.
 */
export const clientLogger = {
  /**
   * Log an informational message.
   */
  info(message: string, properties?: Record<string, unknown>): void {
    const sanitized = sanitizeForLogging(properties) as Record<string, unknown> | undefined;
    console.log(`[INFO] ${message}`, sanitized || "");

    const payload: LogPayload = {
      level: "Information",
      message,
      timestamp: new Date().toISOString(),
      properties: sanitized,
    };
    sendLogToApi(payload);
  },

  /**
   * Log a warning message.
   */
  warn(message: string, properties?: Record<string, unknown>): void {
    const sanitized = sanitizeForLogging(properties) as Record<string, unknown> | undefined;
    console.warn(`[WARN] ${message}`, sanitized || "");

    const payload: LogPayload = {
      level: "Warning",
      message,
      timestamp: new Date().toISOString(),
      properties: sanitized,
    };
    sendLogToApi(payload);
  },

  /**
   * Log an error message.
   */
  error(message: string, error?: Error | string | unknown, properties?: Record<string, unknown>): void {
    let fullMessage = message;
    const props = sanitizeForLogging(properties) as Record<string, unknown> | undefined;

    if (error) {
      const errorStr = formatError(error);
      fullMessage = `${message}: ${errorStr}`;
    }

    console.error(`[ERROR] ${fullMessage}`, props || "");

    const payload: LogPayload = {
      level: "Error",
      message: fullMessage,
      timestamp: new Date().toISOString(),
      properties: props,
    };
    sendLogToApi(payload);
  },

  /**
   * Log a debug message (development only).
   */
  debug(message: string, properties?: Record<string, unknown>): void {
    const sanitized = sanitizeForLogging(properties) as Record<string, unknown> | undefined;
    if (import.meta.env.DEV) {
      console.debug(`[DEBUG] ${message}`, sanitized || "");

      const payload: LogPayload = {
        level: "Debug",
        message,
        timestamp: new Date().toISOString(),
        properties: sanitized,
      };
      sendLogToApi(payload);
    }
  },
};
