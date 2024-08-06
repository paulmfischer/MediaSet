import { JSX } from "preact";
import InputField from "./InputField.tsx";
import { propertyOf } from "../helpers.ts";
import { Book } from "../models.ts";
import Input from "./Input.tsx";
import { Button } from "./Button.tsx";

interface BookFormProps {
  submitText: string;
  book?: Book;
}

export default function BookForm(
  { book, submitText, className, ...props }:
    & JSX.HTMLAttributes<HTMLFormElement>
    & BookFormProps,
) {
  return (
    <form {...props}>
      <div className="flex flex-col gap-2 md:grid md:grid-cols-2 md:gap-4 mb-2">
        {book?.id && (
          <Input
            name={propertyOf<Book>("id")}
            value={book.id}
            hidden="hidden"
          />
        )}
        <InputField label="Title">
          <Input name={propertyOf<Book>("title")} value={book?.title} />
        </InputField>
        <InputField label="Subtitle">
          <Input name={propertyOf<Book>("subtitle")} value={book?.subtitle} />
        </InputField>
        <InputField label="ISBN">
          <Input name={propertyOf<Book>("isbn")} value={book?.isbn} />
        </InputField>
        <InputField label="Format">
          <Input name={propertyOf<Book>("format")} value={book?.format} />
        </InputField>
        <InputField label="Pages">
          <Input
            type="number"
            min="0"
            name={propertyOf<Book>("pages")}
            value={book?.pages}
          />
        </InputField>
        <InputField label="Publication Date">
          <Input
            type="date"
            name={propertyOf<Book>("publicationDate")}
            value={book?.publicationDate}
          />
        </InputField>
        <InputField label="Author">
          <Input
            name={propertyOf<Book>("author")}
            value={book?.author?.join(",")}
          />
        </InputField>
        <InputField label="Publisher">
          <Input
            name={propertyOf<Book>("publisher")}
            value={book?.publisher?.join(",")}
          />
        </InputField>
        <InputField label="Genre">
          <Input
            name={propertyOf<Book>("genre")}
            value={book?.genre?.join(",")}
          />
        </InputField>
        <InputField label="Plot">
          <Input
            type="textarea"
            name={propertyOf<Book>("plot")}
            value={book?.plot}
          />
        </InputField>
      </div>
      <Button className="mt-4 md:mt-0 min-w-24" type="submit">{submitText}</Button>
    </form>
  );
}
