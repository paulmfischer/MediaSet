import IconTrash from "https://deno.land/x/tabler_icons_tsx@0.0.2/tsx/trash.tsx";
import { useState } from "preact/hooks";
import { Button } from "../components/Button.tsx";
import Dialog from "../components/Dialog.tsx";

interface DeleteBookProps {
    bookId: number;
    bookTitle: string;
}

export default function DeleteBook(props: DeleteBookProps) {
    const [displayModal, setDisplayModel] = useState(false);

    const handleDisplayModal = () => setDisplayModel(true);
    const handleModalConfirm = (confirmDelete: boolean) => {
        console.log('confirm delete', confirmDelete, props.bookId);
        setDisplayModel(false);
    };

    return <>
        <Button type='button' onClick={handleDisplayModal}>
            <IconTrash class='w-5 h-5' />
        </Button>
        <div>
            {displayModal && 
                <Dialog>
                    Are you sure want to delete <span class="italic">{`${props.bookTitle} (${props.bookId})`}</span>?
                    <div class="flex flex-row gap-4 justify-center mt-2">
                        <Button class="w-16 justify-center" onClick={() => handleModalConfirm(true)}>Yes</Button>
                        <Button class="w-16 justify-center" onClick={() => handleModalConfirm(false)}>No</Button>
                    </div>
                </Dialog>
            }
        </div>
    </>;
}