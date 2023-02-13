import { Head } from "$fresh/runtime.ts";
import Counter from "../islands/Counter.tsx";
import IconBooks from "tabler-icons"
import { IconAnchor } from "../components/IconAnchor.tsx";
import { Header } from "../components/Header.tsx";
import Layout from "../components/Layout.tsx";

export default function Home({ route }) {
  return (
    <Layout route={route}>
      My Media Sets:
      <div class="flex flex-row p-6">
        <IconAnchor href="/books">
          <IconBooks class="w-6 h-6" />
        </IconAnchor>
      </div>
    </Layout>
  );
}

{/* <>
  <Head>
    <title>My Media Set</title>
  </Head>
  <div class="p-4 mx-auto max-w-screen-md">
    <Header active={route} />
    <Counter start={3} />
    <p class="my-6">
      My Media Sets:
      <div class="flex flex-row p-6">
        <IconAnchor href="/books">
          <IconBooks class="w-6 h-6" />
        </IconAnchor>
      </div>
    </p>
  </div>
</> */}
