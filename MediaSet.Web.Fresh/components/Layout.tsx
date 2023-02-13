import { Head } from "$fresh/runtime.ts";
import { Header } from "../components/Header.tsx";

type Props = {
  route: string;
  title: string;
  children: any;
};

export default function Layout({ route, title, children }: Props) {
  const siteTitle = `My Media Set${title ? ' - ' + title : ''}`;
  return (
    <>
      <Head>
        <title>{siteTitle}</title>
      </Head>
      <div class="p-4 mx-auto max-w-screen-md">
        <Header active={route} />
        <div class="my-6 mx-8">
          {children}
        </div>
      </div>
    </>
  );
}