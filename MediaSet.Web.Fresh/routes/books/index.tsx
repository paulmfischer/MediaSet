import Layout from "../../components/Layout.tsx";

export default function Books({ route }) {
  return (
    <Layout route={route}>
      Book collection
      <div>
        list of books here
      </div>
    </Layout>
  );
}
