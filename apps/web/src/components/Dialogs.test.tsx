import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, it, vi } from 'vitest';
import { TodoFormDialog } from './Dialogs';
it('only sends editable fields when editing an existing task', async () => {
  const save = vi.fn().mockResolvedValue(undefined);
  render(<TodoFormDialog todo={{ id: 'id', title: 'Existing', description: '', priority: 'high', dueDate: null, status: 'completed', completedAt: null, createdAt: '', updatedAt: '' }} onClose={() => {}} onSave={save} />);
  await userEvent.click(screen.getByRole('button', { name: '儲存任務' }));
  expect(save).toHaveBeenCalledWith({ title: 'Existing', description: '', priority: 'high', dueDate: null });
});
it('validates a blank title without submitting', async () => {
  const save = vi.fn(); render(<TodoFormDialog todo={null} onClose={() => {}} onSave={save} />);
  await userEvent.click(screen.getByRole('button', { name: '儲存任務' }));
  expect(screen.getByText('標題須為 1～120 字元')).toBeVisible(); expect(save).not.toHaveBeenCalled();
});
it('retains input when the server fails', async () => {
  render(<TodoFormDialog todo={null} onClose={() => {}} onSave={async () => { throw new Error('連線失敗'); }} />);
  await userEvent.type(screen.getByLabelText(/任務標題/), '保留內容');
  await userEvent.click(screen.getByRole('button', { name: '儲存任務' }));
  await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('連線失敗'));
  expect(screen.getByLabelText(/任務標題/)).toHaveValue('保留內容');
});
