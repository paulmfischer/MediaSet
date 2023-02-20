import { ErrorPageProps } from "$fresh/server.ts";
import Layout from "../components/Layout.tsx";

export default function Error500Page({ error }: ErrorPageProps) {
  return <>
    <Layout route={'/error'}>
      <div>
        Something broke!
      </div>
      <p>
        {(error as Error).message}
      </p>
    </Layout>
  </>
}