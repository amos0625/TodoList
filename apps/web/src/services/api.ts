export type Status = 'pending' | 'completed';
export type Priority = 'low' | 'medium' | 'high';
export interface Todo {
  id: string; title: string; description: string; status: Status; priority: Priority;
  dueDate: string | null; completedAt: string | null; createdAt: string; updatedAt: string;
}
export type TodoDraft = Pick<Todo, 'title' | 'description' | 'priority' | 'dueDate'>;
export interface Query { q: string; status: 'all' | Status; priority: 'all' | Priority; sortBy: 'createdAt' | 'dueDate' | 'priority'; order: 'asc' | 'desc'; page: number; pageSize: number }
export interface ListResult { data: Todo[]; meta: { page: number; pageSize: number; total: number; totalPages: number } }
export interface Stats { total: number; pending: number; completed: number }
export const defaults: Query = { q: '', status: 'all', priority: 'all', sortBy: 'createdAt', order: 'desc', page: 1, pageSize: 20 };
export function readQuery(search: string): Query {
  const p = new URLSearchParams(search);
  const choice = <T extends string>(key: string, values: readonly T[], fallback: T): T => values.includes(p.get(key) as T) ? p.get(key) as T : fallback;
  const number = (key: string, fallback: number, max: number) => {
    const text = p.get(key) ?? '';
    const n = Number(text);
    return /^\d+$/.test(text) && Number.isSafeInteger(n) && n >= 1 && n <= max ? n : fallback;
  };
  return { q: [...(p.get('q') ?? '').trim()].slice(0, 100).join(''),
    status: choice('status', ['all', 'pending', 'completed'], 'all'),
    priority: choice('priority', ['all', 'low', 'medium', 'high'], 'all'),
    sortBy: choice('sortBy', ['createdAt', 'dueDate', 'priority'], 'createdAt'),
    order: choice('order', ['asc', 'desc'], 'desc'), page: number('page', 1, 2147483647), pageSize: number('pageSize', 20, 100) };
}
export const queryString = (q: Query) => new URLSearchParams(Object.entries(q).map(([k, v]) => [k, String(v)])).toString();
export class ApiError extends Error {
  constructor(message: string, public details: { field: string; message: string }[] = [], public uncertain = false) { super(message); }
}
export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    const timeout = AbortSignal.timeout(15000);
    response = await fetch(`/api/v1${path}`, { ...options, headers: { 'Content-Type': 'application/json', ...options.headers }, signal: options.signal ? AbortSignal.any([options.signal, timeout]) : timeout });
  } catch (error) {
    if (options.signal?.aborted) throw error;
    throw new ApiError(options.method ? '無法確認操作結果，請先重新載入確認，再決定是否重試。' : '連線失敗，請檢查網路後重試。', [], !!options.method);
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new ApiError(body?.error?.message ?? '服務暫時無法使用，請稍後重試。', body?.error?.details ?? []);
  }
  return response.status === 204 ? undefined as T : response.json();
}
export function isOverdue(todo: Pick<Todo, 'status' | 'dueDate'>, now = new Date()) {
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  return todo.status === 'pending' && !!todo.dueDate && todo.dueDate < today;
}
export function validateDraft(draft: TodoDraft): Record<string, string> {
  const errors: Record<string, string> = {};
  if ([...draft.title.trim()].length < 1 || [...draft.title.trim()].length > 120) errors.title = '標題須為 1～120 字元';
  if ([...draft.description].length > 2000) errors.description = '備註最多 2,000 字元';
  if (draft.dueDate) {
    const date = new Date(`${draft.dueDate}T00:00:00Z`);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(draft.dueDate) || draft.dueDate.startsWith('0000') || Number.isNaN(date.getTime()) || date.toISOString().slice(0, 10) !== draft.dueDate) errors.dueDate = '請提供有效日期';
  }
  return errors;
}
