import type { MetaFunction } from "@remix-run/node";

export const meta: MetaFunction = () => {
  return [
    { title: "MediaSet" },
    { name: "description", content: "Welcome to your MediaSet!" },
  ];
};

export default function Index() {
  return (
    <div>
      This is the MediaSet home screen!
      <br />
      This will contains stats at some point.
    </div>
  );
}
