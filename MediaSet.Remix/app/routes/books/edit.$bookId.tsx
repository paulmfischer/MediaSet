import type { MetaFunction } from "@remix-run/node";

export const meta: MetaFunction = () => {
  return [
    { title: "Book Edit" },
    { name: "description", content: "Edit a book" },
  ];
};

export default function Edit() {
  return (
    <div className="flex flex-col">
      <div className="flex flex-row items-center justify-between">
        <div className="flex flex-row gap-4 items-end">
          Book edit!
        </div>
        <div className="flex flex-row gap-4">
          <input placeholder="Search" className="p-1 pl-2 dark:text-slate-800" />
        </div>
      </div>
      <div className="h-full mt-4">
        Book Edit
      </div>
    </div>
  );
};