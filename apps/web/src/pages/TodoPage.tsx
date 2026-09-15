import { useCallback, useEffect, useRef, useState } from 'react';
import { ConfirmDeleteDialog, TodoFormDialog } from '../components/Dialogs';
import { defaults, isOverdue, queryString, readQuery, request, type ListResult, type Query, type Stats, type Todo, type TodoDraft } from '../services/api';
import s from '../styles/App.module.css';

const priorityLabels = { low: '低優先', medium: '中優先', high: '高優先' };
export default function TodoPage() {
  const [query, setQuery] = useState(() => readQuery(location.search));
  const [search, setSearch] = useState(query.q);
  const [result, setResult] = useState<ListResult | null>(null);
  const [stats, setStats] = useState<Stats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [editor, setEditor] = useState<{ todo: Todo | null } | null>(null);
  const [deleting, setDeleting] = useState<Todo | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const mutation = useRef(false);
  const saved = useRef(false);
  const requestVersion = useRef(0);

  const changeQuery = useCallback((patch: Partial<Query>) => setQuery(q => ({ ...q, ...patch, page: patch.page ?? 1 })), []);
  useEffect(() => {
    const timeout = setTimeout(() => { const value = [...search.trim()].slice(0, 100).join(''); if (value !== query.q) changeQuery({ q: value }); }, 300);
    return () => clearTimeout(timeout);
  }, [search, query.q, changeQuery]);
  useEffect(() => {
    const back = () => { const next = readQuery(location.search); setSearch(next.q); setQuery(next); };
    addEventListener('popstate', back); return () => removeEventListener('popstate', back);
  }, []);
  useEffect(() => {
    history.replaceState(null, '', `?${queryString(query)}`);
    const controller = new AbortController();
    const version = ++requestVersion.current;
    setLoading(true); setError('');
    Promise.all([
      request<ListResult>(`/todos?${queryString(query)}`, { signal: controller.signal }),
      request<{ data: Stats }>('/todos/stats', { signal: controller.signal }),
    ]).then(([list, summary]) => {
      if (version !== requestVersion.current) return;
      if (query.page > Math.max(1, list.meta.totalPages)) { changeQuery({ page: Math.max(1, list.meta.totalPages) }); return; }
      setResult(list); setStats(summary.data); saved.current = false;
    }).catch(e => {
      if (version !== requestVersion.current || controller.signal.aborted) return;
      setError(saved.current ? '已儲存，列表更新失敗。請重新載入。' : e instanceof Error ? e.message : '載入失敗');
    }).finally(() => { if (version === requestVersion.current && !controller.signal.aborted) setLoading(false); });
    return () => { controller.abort(); };
  }, [query, revision, changeQuery]);

  const refreshAfterSave = (message: string) => {
    ++requestVersion.current;
    saved.current = true; setNotice(message); setRevision(v => v + 1);
  };
  const save = async (draft: TodoDraft) => {
    await request(editor?.todo ? `/todos/${editor.todo.id}` : '/todos', { method: editor?.todo ? 'PATCH' : 'POST', body: JSON.stringify(draft) });
    setEditor(null); refreshAfterSave('任務已儲存');
  };
  const toggle = async (todo: Todo) => {
    if (mutation.current) return;
    mutation.current = true; setBusyId(todo.id); setNotice('');
    try { await request(`/todos/${todo.id}`, { method: 'PATCH', body: JSON.stringify({ status: todo.status === 'pending' ? 'completed' : 'pending' }) }); refreshAfterSave(todo.status === 'pending' ? '又完成一件事，做得好！' : '已恢復為未完成'); }
    catch (e) { setError(e instanceof Error ? e.message : '更新失敗'); }
    finally { mutation.current = false; setBusyId(null); }
  };
  const filtered = !!query.q || query.status !== 'all' || query.priority !== 'all';
  const clear = () => { setSearch(''); setQuery({ ...defaults }); };
  return <main className={s.page}>
    <header className={s.header}><div><div className={s.eyebrow}><span className={s.logo}>✓</span> EVERYDAY / 每日待辦</div><h1>把今天整理好<span className={s.dot}>.</span></h1><p className={s.subtitle}>留一點空間，專注在重要的事。</p></div><button id="add-todo" className={s.primary} onClick={e => { e.currentTarget.focus(); setEditor({ todo: null }); }}>＋ 新增任務</button></header>
    <section aria-label="全部任務統計" className={s.summary}>
      <div><span>全部任務</span><strong>{stats?.total ?? '—'}</strong><small>每一步，都算數</small></div>
      <div><span><i className={s.pendingDot} />未完成</span><strong>{stats?.pending ?? '—'}</strong><small>一步一步來</small></div>
      <div><span><i className={s.completeDot} />已完成</span><strong>{stats?.completed ?? '—'}</strong><small>值得給自己一個肯定</small></div>
    </section>
    <section className={s.workspace} aria-labelledby="list-heading">
      <div className={s.sectionTitle}><h2 id="list-heading">我的任務</h2><span className={s.muted}>讓計畫，成為進展</span></div>
      <div className={s.searchBox}><span aria-hidden="true">⌕</span><input type="search" aria-label="搜尋任務" placeholder="搜尋標題或備註…" value={search} onChange={e => setSearch(e.target.value)} /></div>
      <div className={s.toolbar}><div className={s.tabs} role="group" aria-label="任務狀態">{(['all', 'pending', 'completed'] as const).map((status, i) => <button key={status} aria-pressed={query.status === status} className={query.status === status ? s.activeTab : ''} onClick={() => changeQuery({ status })}>{['全部', '未完成', '已完成'][i]}</button>)}</div>
        <div className={s.filters}><select aria-label="篩選優先級" value={query.priority} onChange={e => changeQuery({ priority: e.target.value as Query['priority'] })}><option value="all">所有優先級</option><option value="high">高優先</option><option value="medium">中優先</option><option value="low">低優先</option></select>
          <select aria-label="排序欄位" value={query.sortBy} onChange={e => changeQuery({ sortBy: e.target.value as Query['sortBy'] })}><option value="createdAt">建立時間</option><option value="dueDate">到期日</option><option value="priority">優先級</option></select><button aria-label={query.order === 'desc' ? '目前降冪，切換升冪' : '目前升冪，切換降冪'} onClick={() => changeQuery({ order: query.order === 'asc' ? 'desc' : 'asc' })}>{query.order === 'desc' ? '↓ 降冪' : '↑ 升冪'}</button></div>
      </div>
      <div aria-live="polite" className={s.notice}>{notice}</div>
      {error && <div role="alert" className={s.error}>{error}<button onClick={() => setRevision(v => v + 1)}>重新載入</button></div>}
      <div aria-busy={loading}>
        {loading ? <div role="status" aria-label="載入中" className={s.skeleton}>{[0, 1, 2].map(i => <div key={i} />)}</div> : !error && result?.data.length === 0 ? <div className={s.empty}><span aria-hidden="true">✓</span><h3>{filtered ? '沒有符合條件的任務' : '從一件小事開始'}</h3><p>{filtered ? '試著調整關鍵字，或清除篩選條件。' : '把腦中的待辦寫下來，為今天留一點餘裕。'}</p><button className={s.primary} onClick={filtered ? clear : () => setEditor({ todo: null })}>{filtered ? '清除篩選' : '新增第一個任務'}</button></div> :
          <ul className={s.list}>{result?.data.map(todo => <li key={todo.id} className={`${s.item} ${todo.status === 'completed' ? s.completed : ''}`}>
            <label className={s.check}><input type="checkbox" aria-label={`完成任務：${todo.title}`} checked={todo.status === 'completed'} disabled={!!busyId || !!error} onChange={() => toggle(todo)} /></label>
            <div className={s.itemContent}><div className={s.itemHeading}><h3>{todo.title}</h3><span className={`${s.badge} ${s[todo.priority]}`}>{priorityLabels[todo.priority]}</span></div>{todo.description && <p>{todo.description}</p>}<div className={s.itemMeta}>{todo.dueDate && <span className={isOverdue(todo) ? s.overdue : ''}>◷ {todo.dueDate}{isOverdue(todo) ? ' · 已逾期' : ' 到期'}</span>}{todo.status === 'completed' && <span className={s.doneText}>✓ 已完成</span>}</div></div>
            <div className={s.itemActions}><button disabled={!!busyId || !!error} aria-label={`編輯：${todo.title}`} onClick={e => { e.currentTarget.focus(); setEditor({ todo }); }}>編輯</button><button disabled={!!busyId || !!error} aria-label={`刪除：${todo.title}`} onClick={e => { e.currentTarget.focus(); setDeleting(todo); }}>刪除</button></div>
          </li>)}</ul>}
      </div>
      <footer className={s.pagination}><span>共 {result?.meta.total ?? 0} 筆{filtered ? '符合條件的任務' : '任務'}</span><div><button disabled={loading || query.page <= 1} onClick={() => changeQuery({ page: query.page - 1 })}>上一頁</button><span>{query.page} / {Math.max(1, result?.meta.totalPages ?? 1)}</span><button disabled={loading || query.page >= (result?.meta.totalPages ?? 1)} onClick={() => changeQuery({ page: query.page + 1 })}>下一頁</button></div></footer>
    </section><footer className={s.pageFooter}>少一點掛心，多一點專注。<span>TODO LIST</span></footer>
    {editor && <TodoFormDialog todo={editor.todo} onClose={() => setEditor(null)} onSave={save} />}
    {deleting && <ConfirmDeleteDialog todo={deleting} onClose={() => setDeleting(null)} onDelete={async () => { await request(`/todos/${deleting.id}`, { method: 'DELETE' }); setDeleting(null); refreshAfterSave('任務已刪除'); }} />}
  </main>;
}
