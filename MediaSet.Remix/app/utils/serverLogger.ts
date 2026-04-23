import { initializeRequestContext, getRequestContext } from './apiFetch.server';

const SEQ_ENABLED = process.env['ExternalLogging__Enabled'] === 'true';
const SEQ_URL = process.env['ExternalLogging__SeqUrl'];

type LogLevel = 'Debug' | 'Information' | 'Warning' | 'Error';

// Information is the CLEF default — omit @l for info-level events
const CLEF_LEVEL: Partial<Record<LogLevel, string>> = {
  Debug: 'Debug',
  Warning: 'Warning',
  Error: 'Error',
};

async function sendLogToSeq(level: LogLevel, message: string, properties?: Record<string, unknown>): Promise<void> {
  if (!SEQ_ENABLED || !SEQ_URL) return;

  const context = getRequestContext();

  const event: Record<string, unknown> = {
    '@t': new Date().toISOString(),
    '@mt': '{Message}',
    '@m': message,
    ...(CLEF_LEVEL[level] !== undefined ? { '@l': CLEF_LEVEL[level] } : {}),
    Application: 'MediaSet.Remix',
    Environment: process.env.NODE_ENV ?? 'development',
    Message: message,
    ...properties,
  };

  if (context?.traceId) {
    // @tr is the CLEF standard field Seq uses for trace correlation (enables the Trace button)
    event['@tr'] = context.traceId;
  }

  try {
    const response = await fetch(`${SEQ_URL}/api/events/raw`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/vnd.serilog.clef' },
      body: JSON.stringify(event),
    });

    if (!response.ok) {
      console.warn(`[ServerLogger] Seq returned ${response.status} when logging`);
    }
  } catch (error) {
    console.warn('[ServerLogger] Failed to send log to Seq:', error);
  }
}

export const serverLogger = {
  info(message: string, properties?: Record<string, unknown>): void {
    initializeRequestContext();
    console.log(`[INFO] ${message}`, properties || '');
    sendLogToSeq('Information', message, properties);
  },

  warn(message: string, properties?: Record<string, unknown>): void {
    initializeRequestContext();
    console.warn(`[WARN] ${message}`, properties || '');
    sendLogToSeq('Warning', message, properties);
  },

  error(message: string, error?: Error | string | unknown, properties?: Record<string, unknown>): void {
    initializeRequestContext();
    let fullMessage = message;
    let mergedProperties = properties;

    if (error !== undefined && error !== null) {
      if (error instanceof Error) {
        fullMessage = `${message}: ${error.name}: ${error.message}`;
      } else if (error instanceof Response) {
        fullMessage = `${message}: HTTP ${error.status} ${error.statusText}`.trimEnd();
      } else if (typeof error === 'string') {
        fullMessage = `${message}: ${error}`;
      } else if (typeof error === 'object') {
        // Plain object passed as second arg — treat as properties, not an error to stringify
        mergedProperties = { ...(error as Record<string, unknown>), ...properties };
      } else {
        fullMessage = `${message}: ${String(error)}`;
      }
    }

    console.error(`[ERROR] ${fullMessage}`, mergedProperties || '');
    sendLogToSeq('Error', fullMessage, mergedProperties);
  },

  debug(message: string, properties?: Record<string, unknown>): void {
    initializeRequestContext();
    console.debug(`[DEBUG] ${message}`, properties || '');
    sendLogToSeq('Debug', message, properties);
  },
};
