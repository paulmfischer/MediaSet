// These settings are read only on the server. Never serialize API_URL or process.env.
export function privateApiUrl() {
  return process.env.API_URL || 'http://127.0.0.1:5100';
}

export function publicApiUrl() {
  return process.env.PUBLIC_API_URL || '/dev-api';
}
