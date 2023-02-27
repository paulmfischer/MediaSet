import IconTrash from "https://deno.land/x/tabler_icons_tsx@0.0.2/tsx/trash.tsx";
import { useState } from "preact/hooks";
import { Button } from "../components/Button.tsx";

interface DeleteBookProps {
    bookId: number;
}

export default function DeleteBook(props: DeleteBookProps) {
    const [displayModal, setDisplayModel] = useState(false);
    const [count, setCount] = useState(0);

    const handleDisplayModal = () => { 
        setCount(count + 1);
        setDisplayModel(true);
    };
    const handleModalConfirm = (confirmDelete: boolean) => {
        // if (delete) {
            // }

        console.log('confirm delete', confirmDelete, props.bookId);
        setDisplayModel(false);
    };

    return <>
        <Button type='button' onClick={handleDisplayModal}>
            <IconTrash class='w-5 h-5' />
        </Button>
        {count}
        {/* {displayModal &&  */}
            <div style={displayModal ? '' : 'display: none;'}>
                Are you sure?
                {/* <Button onClick={() => handleModalConfirm(true)}>Yes</Button>
                <Button onClick={() => handleModalConfirm(false)}>No</Button> */}
            </div>
        {/* } */}
    </>;
}