import { authenticatedFetch } from './authClient'

export async function adminApi(path: string, method = 'GET', body?: unknown, signal?: AbortSignal) {
 const headers: Record<string, string> = {};
 if (body !== undefined) headers['Content-Type'] = 'application/json';
 const result = await authenticatedFetch('/api/' + path, { method, headers, signal, body: body === undefined ? undefined : JSON.stringify(body) });
 if (!result.ok) { const error = await result.json().catch(() => null); throw new Error(error?.message ?? (error?.errors ? Object.values(error.errors).flat().join(' ') : 'Không thực hiện được. Kiểm tra quyền và thông tin nhập.')) }
 return result.status === 204 ? null : result.json();
}
