import { Handlers, PageProps } from "$fresh/server.ts";
import { Book } from "../../models.ts";
import { baseUrl } from "../../constants.ts";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import MediaField from "../../components/MediaField.tsx";
import { Anchor } from "../../components/Anchor.tsx";
import { Button } from "../../components/Button.tsx";
import { TbEdit, TbTrash } from '@preact-icons/tb';

export const handler: Handlers<Book> = {
  async GET(_req, ctx) {
    const response = await fetch(`${baseUrl}/books/${ctx.params.id}`);
    if (!response.ok) {
      return ctx.renderNotFound();
    }
    const book = await response.json();
    return ctx.render(book);
  },
  async POST(req, ctx) {
    const formData = await req.formData();
    const id = formData.get('id') ?? '';

    const response = await fetch(`${baseUrl}/books/${id}`, { method: 'DELETE' });
    if (!response.ok) {
      return ctx.render();
    }

    // on success of delete, reload books page to refresh data
    return new Response('', {
      status: 303,
      headers: { Location: `/books/${id}` }
    });
  }
};

export default function View(props: PageProps<Book>) {
  const book = props.data;
  return (
    <div>
      <div className="flex items-center justify-between border-b dark:border-slate-300 pb-2">
        <MediaHeader title={book.title} />
        <div class="flex gap-4">
          <Anchor href={`/books/${book.id}/edit`} className="flex gap-2">
            <TbEdit size={24} />
            <span>Edit</span>
          </Anchor>
          <form method="POST">
            <input
              hidden="hidden"
              value={book.id}
              name="id"
            />
            <Button type="submit" displayStyle="link" className="flex gap-2 items-center">
              <TbTrash size={24} />
              <span>Delete</span>
            </Button>
          </form>
        </div>
      </div>
      <div className="mt-4 flex flex-col gap-2">
        <MediaField id="subtitle" label="Subtitle" fieldContent={book.subtitle} />
        <MediaField id="format" label="Format" fieldContent={book.format} />
        <MediaField id="pages" label="Pages" fieldContent={book.pages} />
        <MediaField id="publicationDate" label="Publication Date" fieldContent={book.publicationDate} />
        <MediaField id="author" label="Author" fieldContent={book.author?.join(',')} />
        <MediaField id="publisher" label="Publisher" fieldContent={book.publisher?.join(',')} />
        <MediaField id="genre" label="Genre" fieldContent={book.genre?.join(',')} />
        <MediaField id="isbn" label="ISBN" fieldContent={book.isbn} />
        <MediaField id="plot" label="Plot" fieldContent={book.plot} />
      </div>
    </div>
  );
}