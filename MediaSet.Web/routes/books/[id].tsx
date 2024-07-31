import { Handlers, PageProps } from "$fresh/server.ts";
import { Book } from "../../models.ts";
import { baseUrl } from "../../constants.ts";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import MediaField from "../../components/MediaField.tsx";

export const handler: Handlers<Book> = {
  async GET(_req, ctx) {
    const response = await fetch(`${baseUrl}/books/${ctx.params.id}`);
    if (!response.ok) {
      return ctx.renderNotFound();
      // {
      //   message: "Project does not exist",
      // });
    }
    const book = await response.json();
    return ctx.render(book);
  },
};

export default function ProjectPage(props: PageProps<Book>) {
  const book = props.data;
  return (
    <div>
      <div className="flex items-center justify-between">
        <MediaHeader title={book.title} />
      </div>
      <div className="mt-4">
        <MediaField id="subtitle" label="Subtitle" fieldContent={book.subTitle} />
        <MediaField id="format" label="Format" fieldContent={book.format} />
        <MediaField id="pages" label="Pages" fieldContent={book.pages} />
        <MediaField id="publicationDate" label="Publication Date" fieldContent={book.publicationDate} />
        <MediaField id="author" label="Author" fieldContent={book.author.join(',')} />
        <MediaField id="publisher" label="Publisher" fieldContent={book.publisher.join(',')} />
        <MediaField id="genre" label="Genre" fieldContent={book.genre.join(',')} />
        <MediaField id="isbn" label="ISBN" fieldContent={book.isbn} />
        <MediaField id="plot" label="Plot" fieldContent={book.plot} />
      </div>
    </div>
  );
}