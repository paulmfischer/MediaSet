import { Book } from "./models.ts";

export function propertyOf<TObj>(name: keyof TObj) {
  return name;
}

export function getFormData(form: FormData, property: keyof Book): string | undefined {
  const value = form.get(property);
  if (value == null || value == '') {
    return undefined;
  }

  return value as string;
}