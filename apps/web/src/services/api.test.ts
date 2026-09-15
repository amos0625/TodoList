import { describe, expect, it } from 'vitest';
import { readQuery, validateDraft, isOverdue } from './api';
describe('input and dates', () => {
  it('normalizes invalid URL values', () => {
    const query = readQuery('?page=-3&pageSize=101&status=bad&order=bad');
    expect(query.page).toBe(1); expect(query.pageSize).toBe(20); expect(query.status).toBe('all'); expect(query.order).toBe('desc');
  });
  it('counts Unicode code points', () => {
    const draft = { title: '😀'.repeat(120), description: '', priority: 'medium' as const, dueDate: null };
    expect(validateDraft(draft)).toEqual({});
    expect(validateDraft({ ...draft, title: draft.title + '😀' }).title).toBeTruthy();
  });
  it('rejects nonexistent dates', () => {
    expect(validateDraft({ title: 'ok', description: '', priority: 'low', dueDate: '2025-02-29' }).dueDate).toBeTruthy();
    expect(validateDraft({ title: 'ok', description: '', priority: 'low', dueDate: '2024-02-29' })).toEqual({});
  });
  it('uses the local calendar and excludes completed and today', () => {
    const now = new Date(2026, 8, 15, 0, 1);
    expect(isOverdue({ status: 'pending', dueDate: '2026-09-14' }, now)).toBe(true);
    expect(isOverdue({ status: 'pending', dueDate: '2026-09-15' }, now)).toBe(false);
    expect(isOverdue({ status: 'completed', dueDate: '2026-09-14' }, now)).toBe(false);
  });
});
