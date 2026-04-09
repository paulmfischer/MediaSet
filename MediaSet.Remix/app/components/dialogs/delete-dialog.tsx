import { Form, useNavigation } from '@remix-run/react';
import { useCallback, useEffect, useRef } from 'react';
import { X } from 'lucide-react';

type DeleteDialogProps = {
  isOpen: boolean;
  onClose: () => void;
  entityTitle?: string;
  deleteAction: string;
};

export default function DeleteDialog({ isOpen, onClose, entityTitle, deleteAction }: DeleteDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const navigation = useNavigation();
  const wasSubmittingRef = useRef(false);
  const handleClose = useCallback(() => {
    onClose?.();
  }, [onClose]);

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (isOpen) {
      dialog.showModal();
    } else {
      dialog.close();
    }
  }, [isOpen]);

  useEffect(() => {
    const isSubmittingThisDialog = navigation.state === 'submitting' && navigation.formAction === deleteAction;

    if (isSubmittingThisDialog) {
      wasSubmittingRef.current = true;
      handleClose();
      return;
    }

    const returnedIdleForThisDialog = wasSubmittingRef.current && navigation.state === 'idle';

    if (returnedIdleForThisDialog) {
      wasSubmittingRef.current = false;
      handleClose();
    }
  }, [navigation.formAction, navigation.state, deleteAction, handleClose]);

  const handleCancel = (event: React.MouseEvent) => {
    event.preventDefault();
    handleClose();
  };

  const handleBackdropClick = (event: React.MouseEvent<HTMLDialogElement>) => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    const rect = dialog.getBoundingClientRect();
    const isInDialog =
      rect.top <= event.clientY &&
      event.clientY <= rect.top + rect.height &&
      rect.left <= event.clientX &&
      event.clientX <= rect.left + rect.width;

    if (!isInDialog) {
      handleClose();
    }
  };

  return (
    // eslint-disable-next-line jsx-a11y/no-noninteractive-element-interactions
    <dialog
      ref={dialogRef}
      onClick={handleBackdropClick}
      onKeyDown={(e) => {
        if (e.key === 'Escape') handleClose();
      }}
      className="backdrop:bg-gray-900 backdrop:bg-opacity-60 bg-zinc-800 text-slate-200 rounded-lg shadow-xl p-0 w-full max-w-md"
    >
      <div className="flex flex-col">
        <div className="flex items-center justify-between border-b border-slate-600 p-4">
          <h2 className="text-xl font-semibold">Confirm Delete</h2>
          <button onClick={handleCancel} className="secondary" aria-label="Close dialog">
            <X size={24} />
          </button>
        </div>
        <div className="p-6">
          <p className="text-slate-300 mb-6">
            Are you sure you want to delete {entityTitle ? <strong>{entityTitle}</strong> : 'this item'}?
          </p>
          <p className="text-slate-400 text-sm mb-6">This action cannot be undone.</p>
          <div className="flex gap-3 justify-end">
            <button onClick={handleCancel} className="secondary">
              Cancel
            </button>
            <Form action={deleteAction} method="post">
              <button type="submit">Delete</button>
            </Form>
          </div>
        </div>
      </div>
    </dialog>
  );
}
