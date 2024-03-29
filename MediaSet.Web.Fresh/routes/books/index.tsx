import { Handlers, PageProps } from '$fresh/server.ts';
import { Anchor } from '../../components/Anchor.tsx';
import { IconAnchor } from '../../components/IconAnchor.tsx';
import Layout from '../../components/Layout.tsx';
import IconPlus from 'tabler-icons/plus.tsx';
import IconEdit from 'tabler-icons/edit.tsx';
import { BookItem } from '../../models/book.ts';
import moment from 'moment';
import { load } from 'std';
import DeleteBook from '../../islands/DeleteBook.tsx';

const env = await load();
const apiUrl = env['API_URL'];

export const handler: Handlers<Array<BookItem>> = {
  async GET(_, context) {
    const response = await fetch(`${apiUrl}/books`);
    const books: Array<BookItem> = await response.json();

    return context.render(books);
  },
};

export default function Books(props: PageProps<Array<BookItem>>) {
  return (
    <Layout route={props.route} title='Books'>
      <div class='flex flex-col'>
        <div class='flex mb-4'>
          <IconAnchor href='/books/add'>
            <IconPlus className='w-5 h-6' />
          </IconAnchor>
        </div>
        <table class='table-auto'>
          <thead>
            <tr>
              <th class='bg-gray-200 font-medium text-left p-2 rounded-tl-lg w-4'>ID</th>
              <th class='bg-gray-200 font-medium text-left p-2'>Title</th>
              <th class='hidden md:table-cell bg-gray-200 font-medium text-left p-2'>Number of Pages</th>
              <th class='hidden md:table-cell bg-gray-200 font-medium text-left p-2'>Publish Date</th>
              <th class='bg-gray-200 font-medium text-left p-2 rounded-tr-lg'>Actions</th>
            </tr>
          </thead>
          <tbody>
            {props.data.map((book, index) => (
              <tr class={`${index % 2 === 1 ? 'bg-gray-50' : ''}`}>
                <td class='pl-2 border-b border-l'>
                  <Anchor href={`/books/${book.id}`} removeColored>{book.id}</Anchor>
                </td>
                <td class='pl-2 border-b'>{book.title}</td>
                <td class='hidden md:table-cell pl-2 border-b'>{book.numberOfPages}</td>
                <td class='hidden md:table-cell pl-2 border-b'>
                  {book.publishDate !== '' ? moment(book.publishDate).format('MMMM yyyy') : ''}
                </td>
                <td class='pl-2 border-b border-l border-r w-4'>
                  <div class='flex gap-2'>
                    <DeleteBook apiUrl={apiUrl} bookId={book.id} bookTitle={book.title} />
                    <IconAnchor href={`/books/edit/${book.id}`}>
                      <IconEdit className='w-5 h-5' />
                    </IconAnchor>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Layout>
  );
}
