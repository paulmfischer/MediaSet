// Development launcher: Tailwind CSS watch plus the app server, restarted by
// `node --watch` when server sources change. `--mobile` serves HTTPS using
// DEV_HTTPS_CERT/DEV_HTTPS_KEY. Run through `npm run dev` / `npm run dev:mobile`,
// which load optional overrides from .env.local.
import { spawn, type ChildProcess } from 'node:child_process';

const mobile = process.argv.includes('--mobile');

if (mobile && (!process.env.DEV_HTTPS_CERT || !process.env.DEV_HTTPS_KEY)) {
  console.error(
    'Mobile HTTPS requires DEV_HTTPS_CERT and DEV_HTTPS_KEY in .env.local. See Development/DEVELOPMENT.md#mobile-https-and-barcode-scanning.'
  );
  process.exit(1);
}

const env = { ...process.env, NODE_ENV: 'development', MEDIASET_DEV_HTTPS: mobile ? '1' : '0' };

const children: ChildProcess[] = [
  // `--watch=always` keeps Tailwind running when stdin is not a terminal (VS Code tasks).
  spawn(
    process.execPath,
    [
      'node_modules/tailwindcss/lib/cli.js',
      '-c',
      'tailwind.config.ts',
      '-i',
      'app/tailwind.css',
      '-o',
      'public/app.css',
      '--watch=always',
    ],
    { env, stdio: ['ignore', 'inherit', 'inherit'] }
  ),
  spawn(process.execPath, ['--watch', '--watch-preserve-output', '--import', 'remix/node-tsx', 'server.ts'], {
    env,
    stdio: 'inherit',
  }),
];

let stopping = false;

function stop(code = 0) {
  if (stopping) return;
  stopping = true;
  for (const child of children) child.kill('SIGTERM');
  process.exitCode = code;
}

for (const child of children) {
  child.on('exit', (code) => stop(code ?? 0));
}

process.on('SIGINT', () => stop());
process.on('SIGTERM', () => stop());
