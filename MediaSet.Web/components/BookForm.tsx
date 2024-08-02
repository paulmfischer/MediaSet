import InputField from "./InputField.tsx";
import { propertyOf } from "../helpers.ts";
import { Book } from "../models.ts";
import Input from "./Input.tsx";

export default function BookForm() {
  return (
    <form className="grid grid-cols-2 gap-4">
      <InputField label="Title" name={propertyOf<Book>("title")}>
        <Input />
      </InputField>
      <InputField label="Subtitle" name={propertyOf<Book>("subTitle")}>
        <Input />
      </InputField>
      <InputField label="ISBN" name={propertyOf<Book>("isbn")}>
        <Input />
      </InputField>
      <InputField label="Format" name={propertyOf<Book>("format")}>
        <Input />
      </InputField>
      <InputField label="Pages" name={propertyOf<Book>("pages")}>
        <Input type="number" min="0" />
      </InputField>
      <InputField label="Publication Date" name={propertyOf<Book>("publicationDate")}>
        <Input type="date" />
      </InputField>
      <InputField label="Author" name={propertyOf<Book>("author")}>
        <Input />
      </InputField>
      <InputField label="Publisher" name={propertyOf<Book>("publisher")}>
        <Input />
      </InputField>
      <InputField label="Genre" name={propertyOf<Book>("genre")}>
        <Input />
      </InputField>
      <InputField label="Plot" name={propertyOf<Book>("plot")} inputProps={{ type: 'textarea' }}>
        <Input type="textarea" />
      </InputField>
    </form>
  )
}