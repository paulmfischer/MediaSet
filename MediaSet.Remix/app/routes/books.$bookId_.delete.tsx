import type { ActionFunctionArgs } from "@remix-run/node";
import { redirect } from "@remix-run/node";
import invariant from "tiny-invariant"
import { Entities } from "~/constants";
import { remove } from "~/entity-data";
import { Book } from "~/models";

export const action = async ({ params }: ActionFunctionArgs) => {
  invariant(params.bookId, "Missing bookId param");
  await remove<Book>(Entities.Books, params.bookId);
  return redirect('/books');
};