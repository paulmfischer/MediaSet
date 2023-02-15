import { Handlers, PageProps } from "$fresh/server.ts";
import { Button } from "../../components/Button.tsx";
import { TextInput } from "../../components/TextInput.tsx";
import Layout from "../../components/Layout.tsx";
import { BookItem, NewBookKeys } from "../../models/book.ts";

interface AddBookProps {
  newBook: BookItem;
}

export const handler: Handlers<AddBookProps> = {
  async POST(req, context) {
    const formData = await req.formData();
    const newBook: { [key: string]: unknown } = {};
    formData.forEach((value, key) => newBook[key] = value);
    console.log('add form data', formData, newBook);
    const response = await fetch('http://localhost:5103/books', {
      body: JSON.stringify(newBook),
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      }
    });

    console.log('saved new book!', response);
    return context.render();
  },
};

export default function AddBook(props: PageProps<AddBookProps>) {
  return (
    <Layout route={props.route} title="Add Book">
      Add Book
      <form class="flex flex-col" method="post">
        <TextInput inputLabel="Title" name="title" />
        <TextInput inputLabel="Publish Date" name="publishDate" />
        <TextInput inputLabel="Number of Pages" name="numberOfPages" />
        <TextInput inputLabel="ISBN" name="isbn" />
        <Button type="submit" className="mt-2">Add</Button>
      </form>
    </Layout>
  );
}