import { useSignal } from "@preact/signals";
import Counter from "../islands/Counter.tsx";

export default function Home() {
  const count = useSignal(3);
  return (
    <div class="px-4 py-8 mx-auto">
      <div class="max-w-screen-md mx-auto flex flex-col items-center justify-center">
        <h1 class="text-4xl font-bold">Welcome to MediaSet</h1>
        <p class="my-4">
          Select a type of Media from the navigation.
        </p>
      </div>
    </div>
  );
}
