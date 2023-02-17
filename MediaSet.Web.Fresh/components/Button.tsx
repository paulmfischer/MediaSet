import { JSX } from "preact";

export function Button(props: JSX.HTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      type="button"
      {...props}
      class="bg-white text-gray-500 hover:text-gray-700 rounded border(gray-400 1) hover:bg-gray-200 flex gap-2"
    />
  );
}
