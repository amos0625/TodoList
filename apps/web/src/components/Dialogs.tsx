import { useEffect, useRef, useState, type ReactNode, type RefObject } from 'react';
import { ApiError, validateDraft, type Todo, type TodoDraft, type Priority } from '../services/api';
import s from '../styles/App.module.css';

function Modal({ title, children, onClose, busy, firstFocus }: { title: string; children: ReactNode; onClose: () => void; busy: boolean; firstFocus?: RefObject<HTMLButtonElement | null> }) {
  const ref = useRef<HTMLDialogElement>(null);
  const returnFocus = useRef(document.activeElement as HTMLElement | null);
  useEffect(() => {
    const dialog = ref.current!;
    dialog.showModal();
    (firstFocus?.current ?? dialog.querySelector<HTMLInputElement>('input') ?? dialog.querySelector<HTMLButtonElement>('button'))?.focus();
    return () => { dialog.close(); const previous = returnFocus.current; if (previous?.isConnected) previous.focus(); else document.getElementById('add-todo')?.focus(); };
  }, [firstFocus]);
  return <dialog ref={ref} className={s.dialog} aria-labelledby="dialog-title" onCancel={e => { e.preventDefault(); if (!busy) onClose(); }}>
    <div className={s.dialogHeading}><h2 id="dialog-title">{title}</h2><button type="button" aria-label="關閉" disabled={busy} onClick={onClose}>×</button></div>{children}
  </dialog>;
}

export function TodoFormDialog({ todo, onClose, onSave }: { todo: Todo | null; onClose: () => void; onSave: (draft: TodoDraft) => Promise<void> }) {
  const [draft, setDraft] = useState<TodoDraft>(todo
    ? { title: todo.title, description: todo.description, priority: todo.priority, dueDate: todo.dueDate }
    : { title: '', description: '', priority: 'medium', dueDate: null });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const pending = useRef(false);
  return <Modal title={todo ? '編輯任務' : '新增任務'} onClose={onClose} busy={busy}>
    <form noValidate onSubmit={async e => {
      e.preventDefault(); if (pending.current) return;
      const found = validateDraft(draft); setErrors(found); setMessage('');
      if (Object.keys(found).length) { document.getElementById(Object.keys(found)[0])?.focus(); return; }
      pending.current = true; setBusy(true);
      try { await onSave({ ...draft, title: draft.title.trim() }); }
      catch (error) {
        setMessage(error instanceof Error ? error.message : '儲存失敗');
        if (error instanceof ApiError) setErrors(Object.fromEntries(error.details.map(x => [x.field, x.message])));
      } finally { pending.current = false; setBusy(false); }
    }}>
      <label htmlFor="title">任務標題 <span className={s.required}>*</span></label>
      <input id="title" autoFocus value={draft.title} placeholder="接下來想完成什麼？" aria-invalid={!!errors.title} aria-describedby={errors.title ? 'title-error' : undefined} onChange={e => setDraft({ ...draft, title: e.target.value })} />
      {errors.title && <p id="title-error" className={s.fieldError}>{errors.title}</p>}
      <label htmlFor="description">備註 <span className={s.muted}>選填</span></label>
      <textarea id="description" rows={4} placeholder="補充細節，讓下一步更清楚" value={draft.description} aria-invalid={!!errors.description} aria-describedby={errors.description ? 'description-error' : undefined} onChange={e => setDraft({ ...draft, description: e.target.value })} />
      {errors.description && <p id="description-error" className={s.fieldError}>{errors.description}</p>}
      <div className={s.formRow}><div><label htmlFor="priority">優先級</label><select id="priority" value={draft.priority} onChange={e => setDraft({ ...draft, priority: e.target.value as Priority })}><option value="low">低優先</option><option value="medium">中優先</option><option value="high">高優先</option></select></div>
        <div><label htmlFor="dueDate">到期日 <span className={s.muted}>選填</span></label><input id="dueDate" type="date" value={draft.dueDate ?? ''} aria-invalid={!!errors.dueDate} aria-describedby={errors.dueDate ? 'dueDate-error' : undefined} onChange={e => setDraft({ ...draft, dueDate: e.target.value || null })} />{errors.dueDate && <p id="dueDate-error" className={s.fieldError}>{errors.dueDate}</p>}</div></div>
      {message && <p role="alert" className={s.error}>{message}</p>}
      <div className={s.dialogActions}><button type="button" disabled={busy} onClick={onClose}>取消</button><button className={s.primary} disabled={busy} type="submit">{busy ? '儲存中…' : '儲存任務'}</button></div>
    </form>
  </Modal>;
}

export function ConfirmDeleteDialog({ todo, onClose, onDelete }: { todo: Todo; onClose: () => void; onDelete: () => Promise<void> }) {
  const cancel = useRef<HTMLButtonElement>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const pending = useRef(false);
  return <Modal title="刪除這個任務？" onClose={onClose} busy={busy} firstFocus={cancel}>
    <p className={s.deleteTitle}>{todo.title}</p><p className={s.muted}>刪除後無法復原。</p>
    {error && <p role="alert" className={s.error}>{error}</p>}
    <div className={s.dialogActions}><button ref={cancel} disabled={busy} onClick={onClose}>取消</button><button className={s.danger} disabled={busy} onClick={async () => {
      if (pending.current) return; pending.current = true; setBusy(true);
      try { await onDelete(); } catch (e) { setError(e instanceof Error ? e.message : '刪除失敗'); } finally { pending.current = false; setBusy(false); }
    }}>{busy ? '刪除中…' : '確認刪除'}</button></div>
  </Modal>;
}
