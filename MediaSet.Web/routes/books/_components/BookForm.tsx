import { JSX } from "preact";
import InputField from "../../../components/InputField.tsx";
import { propertyOf } from "../../../helpers.ts";
import { Book } from "../../../models.ts";

export default function BookForm() {
  return (
    <form>
      <InputField label="Title" name={propertyOf<Book>("title")} />
      <InputField label="Subtitle" name={propertyOf<Book>("subTitle")} />
      <InputField label="ISBN" name={propertyOf<Book>("isbn")} />
      <InputField label="Format" name={propertyOf<Book>("format")} />
      <InputField label="Pages" name={propertyOf<Book>("pages")} />
      <InputField label="Publication Date" name={propertyOf<Book>("publicationDate")} />
      <InputField label="Author" name={propertyOf<Book>("author")} />
      <InputField label="Publisher" name={propertyOf<Book>("publisher")} />
      <InputField label="Genre" name={propertyOf<Book>("genre")} />
      <InputField label="Plot" name={propertyOf<Book>("plot")} />
    </form>
  )
}