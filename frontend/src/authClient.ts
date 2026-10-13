let accessToken: string | null = null
let refreshInFlight: Promise<string | null> | null = null
const isPublicAuthRequest = (input: RequestInfo | URL) => /\/auth\/(?:csrf|login|refresh|reset-password)(?:[/?]|$)/.test(String(input))

export function setAccessToken(value: string | null) { accessToken = value }

async function csrfToken(): Promise<string> {
 const response = await fetch('/api/auth/csrf', { cache: 'no-store', credentials: 'same-origin' })
 if (!response.ok) throw new Error('Phiên đăng nhập không hợp lệ.')
 return (await response.json()).token as string
}

export async function authMutation(path: string, body?: unknown): Promise<Response> {
 const headers: Record<string, string> = { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': await csrfToken() }
 if (accessToken) headers.Authorization = `Bearer ${accessToken}`
 return fetch('/api/auth/' + path, { method: 'POST', headers, body: body === undefined ? undefined : JSON.stringify(body), cache: 'no-store', credentials: 'same-origin' })
}

async function refreshAccessToken(): Promise<string | null> {
 if (refreshInFlight) return refreshInFlight
 refreshInFlight = (async () => {
  // Refresh is shared across concurrent API calls. Do not bind this shared
  // request to one caller's AbortSignal: React StrictMode and route changes
  // can abort that caller while another caller still needs the refresh result.
  const csrf = await csrfToken()
  const response = await fetch('/api/auth/refresh', {
   method: 'POST', cache: 'no-store', credentials: 'same-origin',
   headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf }, body: '{}'
  })
  if (!response.ok) { accessToken = null; return null }
  const result = await response.json() as { accessToken: string }
  accessToken = result.accessToken
  return accessToken
 })().finally(() => { refreshInFlight = null })
 return refreshInFlight
}

export async function authenticatedFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
 if (!accessToken && !isPublicAuthRequest(input)) await refreshAccessToken()
 const send = () => {
  const headers = new Headers(init.headers)
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  return fetch(input, { ...init, headers, cache: init.cache ?? 'no-store', credentials: 'same-origin' })
 }
 let response = await send()
 if (response.status === 401 && !isPublicAuthRequest(input)) {
  const refreshed = await refreshAccessToken()
  if (refreshed) response = await send()
 }
 return response
}

export async function establishBrowserSession(response: Response) {
 const result = await response.json() as { accessToken: string }
 setAccessToken(result.accessToken)
}
