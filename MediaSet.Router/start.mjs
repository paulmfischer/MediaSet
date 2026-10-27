import process from 'node:process';

// Keep the isolated shell off the current frontend's port, including in production mode.
process.env.HOST ||= '127.0.0.1';
process.env.PORT ||= '3101';
process.argv[2] = './build/server/index.js';
await import(new URL('./bin.js', import.meta.resolve('@react-router/serve/package.json')).href);
