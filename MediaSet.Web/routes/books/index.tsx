import { Handlers, PageProps } from "$fresh/server.ts";

interface Book {
    id: string;
    title: string;
    genre: string;
    author: string;
    format: string;
}

export const handler: Handlers<Array<Book>> = {
  async GET(_req, ctx) {
    const response = await fetch("https://localhost:7130/books", {
        headers: {
            accept: "application/json",
        }
    });
    // console.log('did we get books response?', response);
    if (!response) {
      return ctx.renderNotFound({
        message: "Project does not exist",
      });
    }
    const books = await response.json();
    // console.log('what are the actual books?', books);
    return ctx.render(books);
  },
};

export default function Books(props: PageProps<Array<Book>>) {
    // console.log('book components', props.data);
  return <div>You have {props.data.length} books.</div>;
}