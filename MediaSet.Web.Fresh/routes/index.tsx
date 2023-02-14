import Layout from "../components/Layout.tsx";
import { IconAnchor } from "../components/IconAnchor.tsx";
import IconBooks from "tabler-icons"

interface HomeProps {
  route: string;
}

export default function Home({ route }: HomeProps) {
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
