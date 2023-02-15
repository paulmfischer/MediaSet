import { PageProps } from "$fresh/server.ts";
import Layout from "../components/Layout.tsx";
import { IconAnchor } from "../components/IconAnchor.tsx";
import IconBooks from "tabler-icons/books.tsx";

export default function Home({ route }: PageProps) {
  return (
    <Layout route={route}>
      My Media Sets:
      <div class="flex flex-row p-6">
        <IconAnchor href="/books">
          <IconBooks class="w-11 h-11" />
        </IconAnchor>
      </div>
    </Layout>
  );
}
