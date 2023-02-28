import IconTrash from 'tabler-icons/trash.tsx';
import { useState } from 'preact/hooks';
import { Button } from '../components/Button.tsx';
import Dialog from '../components/Dialog.tsx';
import { Status } from 'http-status';

interface DeleteBookProps {
  bookId: number;
  bookTitle: string;
  apiUrl: string;
}

export default function DeleteBook(props: DeleteBookProps) {
  const [displayModal, setDisplayModel] = useState(false);
  const [errorDeleting, setErrorDeleting] = useState(false);

  const handleDisplayModal = () => setDisplayModel(true);
  const handleModalConfirm = (confirmDelete: boolean) => {
    if (confirmDelete) {
      deleteBook(props.apiUrl, props.bookId)
        .then(() => {
          setDisplayModel(false);
          setErrorDeleting(true);
          window.location.reload();
        })
        .catch(() => setErrorDeleting(true));
    } else {
      setDisplayModel(false);
    }
  };

  return (
    <>
      <Button type='button' onClick={handleDisplayModal}>
        <IconTrash class='w-5 h-5' />
      </Button>
      <div>
        {displayModal &&
          (
            <Dialog>
              Are you sure want to delete <span class='italic'>{`${props.bookTitle} (${props.bookId})`}</span>?
              {errorDeleting && <div class='text-red-600'>There was an error deleting the book, please try again.</div>}
              <div class='flex flex-row gap-4 justify-center mt-2'>
                <Button class='w-16 justify-center' onClick={() => handleModalConfirm(true)}>Yes</Button>
                <Button class='w-16 justify-center' onClick={() => handleModalConfirm(false)}>No</Button>
              </div>
            </Dialog>
          )}
      </div>
    </>
  );
}

async function deleteBook(apiUrl: string, bookId: number) {
  const response = await fetch(`${apiUrl}/books/${bookId}`, {
    method: 'DELETE',
  });

  if (response.status === Status.OK) {
    return Promise.resolve();
  } else {
    return Promise.reject();
  }
}
