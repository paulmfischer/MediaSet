import { TbBook2 } from "@preact-icons/tb";
import { Anchor } from "../components/Anchor.tsx";

export default function Home() {
  return (
    <div class="px-4 py-8 mx-auto">
      <div class="max-w-screen-md mx-auto flex flex-col items-normal sm:items-center justify-center">
        <h1 class="text-4xl font-bold">Welcome to MediaSet</h1>
        <p class="my-4">
          Your Collections:
          <Anchor href="/books" className="flex items-center mt-2">
            <TbBook2 size={24} />
            <span className="ml-2 text-xl">Books</span>
          </Anchor>
        </p>
      </div>
    </div>
  );
}
