import { Link } from "@remix-run/react";
import { AlertTriangle, Home } from "lucide-react";

interface ErrorScreenProps {
  title: string;
  message?: string;
  statusCode?: number;
  statusText?: string;
  data?: unknown;
  showDetails?: boolean;
  showRetry?: boolean;
  onRetry?: () => void;
}

function formatData(data: unknown): string | undefined {
  if (data === null || data === undefined) {
    return undefined;
  }

  if (typeof data === "string") {
    return data;
  }

  if (data instanceof Error) {
    return data.message;
  }

  try {
    return JSON.stringify(data, null, 2);
  } catch {
    return String(data);
  }
}

export default function ErrorScreen({
  title,
  message,
  statusCode,
  statusText,
  data,
  showDetails = false,
  showRetry = true,
  onRetry,
}: ErrorScreenProps) {
  const dataText = formatData(data);
  const shouldShowDetails = showDetails && (statusCode || statusText || dataText);
  const containerClasses = [
    "w-full max-w-3xl rounded-lg border border-zinc-700",
    "bg-zinc-800/50 p-8 shadow-lg",
  ];
  const actionClasses = [
    "inline-flex items-center justify-center gap-2 rounded-lg bg-emerald-500",
    "px-4 py-2 text-sm font-semibold text-zinc-950 shadow-lg shadow-emerald-500/25 transition",
    "hover:bg-emerald-400 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
    "focus-visible:outline-emerald-300",
  ];
  const secondaryActionClasses = [
    "inline-flex items-center justify-center gap-2 rounded-lg border border-emerald-400/60",
    "px-4 py-2 text-sm font-semibold text-emerald-200 transition",
    "hover:border-emerald-300 hover:text-emerald-100",
    "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2",
    "focus-visible:outline-emerald-300",
  ];

  return (
    <div className="flex w-full justify-center">
      <div className={containerClasses.join(" ")}>
        <div className="flex flex-col gap-6">
          <div className="flex items-start gap-4">
            <div className="mt-1 flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-500/15 text-emerald-300">
              <AlertTriangle size={28} aria-hidden />
            </div>
            <div className="space-y-2">
              <h2 className="text-2xl font-semibold text-white">{title}</h2>
              {message ? (
                <p className="text-base leading-relaxed text-slate-300">{message}</p>
              ) : null}
            </div>
          </div>

          <div className="flex flex-col items-center justify-center gap-3 sm:flex-row sm:gap-4">
            {showRetry ? (
              <button
                type="button"
                onClick={onRetry}
                className={secondaryActionClasses.join(" ")}
              >
                <span>Try Again</span>
              </button>
            ) : null}
            <Link
              to="/"
              className={actionClasses.join(" ")}
            >
              <Home size={18} aria-hidden />
              <span>Go Home</span>
            </Link>
          </div>

          {shouldShowDetails ? (
            <div className="rounded-lg border border-zinc-700 bg-zinc-900/70 p-4">
              <div className="flex items-center justify-between">
                <p className="text-xs font-semibold uppercase tracking-wide text-slate-400">Details</p>
                {statusCode ? (
                  <span className="rounded-full bg-zinc-800 px-3 py-1 text-xs font-semibold text-slate-200">
                    {statusCode}
                  </span>
                ) : null}
              </div>

              <dl className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
                {statusText ? (
                  <div className="rounded-md border border-zinc-800 bg-zinc-900/70 p-3">
                    <dt className="text-xs uppercase tracking-wide text-slate-500">Status</dt>
                    <dd className="mt-1 text-sm text-slate-200">{statusText}</dd>
                  </div>
                ) : null}

                {statusCode ? (
                  <div className="rounded-md border border-zinc-800 bg-zinc-900/70 p-3">
                    <dt className="text-xs uppercase tracking-wide text-slate-500">Code</dt>
                    <dd className="mt-1 text-sm text-slate-200">{statusCode}</dd>
                  </div>
                ) : null}
              </dl>

              {dataText ? (
                <div className="mt-3 rounded-md border border-zinc-800 bg-zinc-950 p-3">
                  <dt className="text-xs uppercase tracking-wide text-slate-500">Message</dt>
                  <dd className="mt-1 text-sm text-slate-100">
                    <pre className="whitespace-pre-wrap break-words font-mono text-xs leading-relaxed text-slate-200">
                      {dataText}
                    </pre>
                  </dd>
                </div>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
