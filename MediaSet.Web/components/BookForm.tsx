import { JSX } from "preact";
import InputField from "./InputField.tsx";
import { propertyOf } from "../helpers.ts";
import { Book } from "../models.ts";
import Input from "./Input.tsx";
import { Button } from "./Button.tsx";

interface BookFormProps {
  submitText: string;
}

export default function BookForm({ submitText, className, ...props }: JSX.HTMLAttributes<HTMLFormElement> & BookFormProps) {
  return (
    <form {...props} className={`grid grid-cols-2 gap-4 ${className}`}>
      <InputField label="Title">
        <Input name={propertyOf<Book>("title")} />
      </InputField>
      <InputField label="Subtitle">
        <Input name={propertyOf<Book>("subTitle")} />
      </InputField>
      <InputField label="ISBN">
        <Input name={propertyOf<Book>("isbn")} />
      </InputField>
      <InputField label="Format">
        <Input name={propertyOf<Book>("format")} />
      </InputField>
      <InputField label="Pages">
        <Input type="number" min="0" name={propertyOf<Book>("pages")} />
      </InputField>
      <InputField label="Publication Date">
        <Input type="date" name={propertyOf<Book>("publicationDate")} />
      </InputField>
      <InputField label="Author">
        <Input name={propertyOf<Book>("author")} />
      </InputField>
      <InputField label="Publisher">
        <Input name={propertyOf<Book>("publisher")} />
      </InputField>
      <InputField label="Genre">
        <Input name={propertyOf<Book>("genre")} />
      </InputField>
      <InputField label="Plot">
        <Input type="textarea" name={propertyOf<Book>("plot")} />
      </InputField>
      <Button type="submit">{submitText}</Button>
    </form>
  )
}