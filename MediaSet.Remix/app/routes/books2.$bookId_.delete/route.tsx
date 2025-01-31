import type { ActionFunctionArgs } from "@remix-run/node";
import { redirect } from "@remix-run/node";
import invariant from "tiny-invariant"
import { deleteEntity } from "~/entity-data";
import { Book, Entity } from "~/models";

export const action = async ({ params }: ActionFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  await deleteEntity<Book>(Entity.Books, params.bookId);
  return redirect('/books');
};