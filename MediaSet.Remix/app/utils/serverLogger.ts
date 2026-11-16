import { initializeRequestContext, getRequestContext } from './apiFetch.server.ts';

const SEQ_ENABLED = process.env['ExternalLogging__Enabled'] === 'true';
const SEQ_URL = process.env['ExternalLogging__SeqUrl'];
const SEQ_API_KEY = process.env['ExternalLogging__SeqApiKey'];

const APPLICATION = 'MediaSet.Remix';

/** SourceContext of events logged through `serverLogger` directly rather than a `createServerLogger` logger. */
const DEFAULT_SOURCE_CONTEXT = APPLICATION;

/** The ASP.NET environment name for a `NODE_ENV` value, so Seq's `Environment` property matches the API's. */
export function seqEnvironmentName(nodeEnv: string | undefined): string {
  switch (nodeEnv?.trim().toLowerCase()) {
    case undefined:
    case '':
    case 'development':
      return 'Development';
    case 'production':
      return 'Production';
    // The API's name for its test environment is `Testing`, not `Test`.
    case 'test':
    case 'testing':
      return 'Testing';
    default: {
      const name = nodeEnv!.trim();
      return name.charAt(0).toUpperCase() + name.slice(1).toLowerCase();
    }
  }
}

type LogLevel = 'Debug' | 'Information' | 'Warning' | 'Error';
type LogProperties = Record<string, unknown>;

// Information is the CLEF default — omit @l for info-level events
const CLEF_LEVEL: Partial<Record<LogLevel, string>> = {
  Debug: 'Debug',
  Warning: 'Warning',
  Error: 'Error',
};

const CONSOLE_PREFIX: Record<LogLevel, string> = {
  Debug: 'DEBUG',
  Information: 'INFO',
  Warning: 'WARN',
  Error: 'ERROR',
};

// A Serilog-style hole: {name}, {@name} or {$name}, optionally with a format after a colon
const HOLE = /\{[@$]?([A-Za-z_][A-Za-z0-9_]*)(?::[^}]*)?\}/g;

function renderValue(value: unknown): string {
  if (typeof value === 'string') return value;
  if (value === undefined) return 'undefined';
  if (value === null || typeof value !== 'object') return String(value);
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

/**
 * Renders a message template with its properties, as Seq does: `{name}` becomes the property's value, and a hole
 * without a property is left as written.
 */
export function renderMessageTemplate(template: string, properties: LogProperties = {}): string {
  return template.replace(HOLE, (hole, name: string) =>
    Object.prototype.hasOwnProperty.call(properties, name) ? renderValue(properties[name]) : hole
  );
}

/**
 * The CLEF event sent to Seq. `@mt` is the template itself, so events group by it; the fixed properties
 * (Application, Environment) are applied last so a caller's property can never replace them.
 */
export function buildSeqEvent(
  level: LogLevel,
  template: string,
  properties: LogProperties = {},
  exception?: string,
  nodeEnv: string | undefined = process.env.NODE_ENV
): Record<string, unknown> {
  return {
    '@t': new Date().toISOString(),
    '@mt': template,
    '@m': renderMessageTemplate(template, properties),
    ...(CLEF_LEVEL[level] !== undefined ? { '@l': CLEF_LEVEL[level] } : {}),
    ...(exception ? { '@x': exception } : {}),
    SourceContext: DEFAULT_SOURCE_CONTEXT,
    ...properties,
    Application: APPLICATION,
    Environment: seqEnvironmentName(nodeEnv),
  };
}

function isControlCharacter(character: string): boolean {
  const code = character.charCodeAt(0);
  return code < 0x20 || code === 0x7f;
}

/** The headers of a Seq ingestion request. The `X-Seq-ApiKey` header is sent only when a non-blank key without control characters is set. */
export function seqRequestHeaders(apiKey: string | undefined): Record<string, string> {
  const trimmed = apiKey?.trim();
  // A key with control characters would make fetch throw on every event, so it is treated as absent
  const key = trimmed && ![...trimmed].some(isControlCharacter) ? trimmed : undefined;
  return {
    'Content-Type': 'application/vnd.serilog.clef',
    ...(key ? { 'X-Seq-ApiKey': key } : {}),
  };
}

async function sendLogToSeq(
  level: LogLevel,
  template: string,
  properties?: LogProperties,
  exception?: string
): Promise<void> {
  if (!SEQ_ENABLED || !SEQ_URL) return;

  const context = getRequestContext();
  const event = buildSeqEvent(level, template, properties, exception);

  if (context?.traceId) {
    // @tr is the CLEF standard field Seq uses for trace correlation (enables the Trace button)
    event['@tr'] = context.traceId;
  }

  try {
    const response = await fetch(`${SEQ_URL}/api/events/raw`, {
      method: 'POST',
      headers: seqRequestHeaders(SEQ_API_KEY),
      body: JSON.stringify(event),
    });

    if (!response.ok) {
      console.warn(`[ServerLogger] Seq returned ${response.status} when logging`);
    }
  } catch (error) {
    console.warn('[ServerLogger] Failed to send log to Seq:', error);
  }
}

/**
 * Splits the `error` argument into the exception text (`@x`) and extra properties. A Response adds its status, a plain
 * object is treated as properties, and anything else becomes the exception text.
 */
interface DescribedError {
  /** The full text sent as `@x` (the stack for an Error). */
  exception?: string;
  /** The one-line form shown on the console. */
  summary?: string;
  properties?: LogProperties;
}

function describeError(error: unknown): DescribedError {
  if (error === undefined || error === null) return {};
  if (error instanceof Error) {
    const summary = `${error.name}: ${error.message}`;
    return { exception: error.stack ?? summary, summary };
  }
  if (error instanceof Response) {
    return { properties: { status: error.status, ...(error.statusText ? { statusText: error.statusText } : {}) } };
  }
  if (typeof error === 'object') return { properties: error as LogProperties };
  return { exception: String(error), summary: String(error) };
}

function write(level: LogLevel, template: string, properties?: LogProperties, error: DescribedError = {}): void {
  initializeRequestContext();
  const rendered = renderMessageTemplate(template, properties);
  const line = error.summary ? `${rendered}: ${error.summary}` : rendered;
  // SourceContext goes to Seq only; on the console it would print a properties object on every line
  const consoleProperties = Object.fromEntries(
    Object.entries(properties ?? {}).filter(([name]) => name !== 'SourceContext')
  );
  const args: [string, LogProperties | string] = [
    `[${CONSOLE_PREFIX[level]}] ${line}`,
    Object.keys(consoleProperties).length > 0 ? consoleProperties : '',
  ];
  if (level === 'Error') console.error(...args);
  else if (level === 'Warning') console.warn(...args);
  else if (level === 'Debug') console.debug(...args);
  else console.log(...args);
  sendLogToSeq(level, template, properties, error.exception);
}

export interface ServerLogger {
  /** `template` is a message template: `{name}` holes are filled from `properties` of the same name. */
  info(template: string, properties?: LogProperties): void;
  warn(template: string, properties?: LogProperties): void;
  error(template: string, error?: unknown, properties?: LogProperties): void;
  debug(template: string, properties?: LogProperties): void;
}

export const serverLogger: ServerLogger = {
  info(template, properties) {
    write('Information', template, properties);
  },

  warn(template, properties) {
    write('Warning', template, properties);
  },

  error(template, error, properties) {
    const described = describeError(error);
    const merged = described.properties ? { ...described.properties, ...properties } : properties;
    write('Error', template, merged, described);
  },

  debug(template, properties) {
    write('Debug', template, properties);
  },
};

/**
 * A logger whose events carry `SourceContext` (the module that logs, such as `app/api/entity-data`), so Seq can
 * group and filter them like the API's. It forwards to `serverLogger`, so a test that replaces a `serverLogger`
 * method still sees these events.
 */
export function createServerLogger(sourceContext: string): ServerLogger {
  const withContext = (properties?: LogProperties): LogProperties => ({ ...properties, SourceContext: sourceContext });
  return {
    info: (template, properties) => serverLogger.info(template, withContext(properties)),
    warn: (template, properties) => serverLogger.warn(template, withContext(properties)),
    error: (template, error, properties) => serverLogger.error(template, error, withContext(properties)),
    debug: (template, properties) => serverLogger.debug(template, withContext(properties)),
  };
}
