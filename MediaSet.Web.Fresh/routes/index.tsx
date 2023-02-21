import { PageProps } from '$fresh/server.ts';
import Layout from '../components/Layout.tsx';
import { IconAnchor } from '../components/IconAnchor.tsx';
import IconBooks from 'tabler-icons/books.tsx';

export default function Home({ route }: PageProps) {
  return (
    <Layout route={route}>
      My Media Sets:
      <div class='flex flex-row p-6'>
        <IconAnchor href='/books' class='w-16 h-16 border-2 flex justify-center items-center'>
          <IconBooks class='w-12 h-12' />
        </IconAnchor>
      </div>
    </Layout>
  );
}
