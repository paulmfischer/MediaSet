import { Handlers, type PageProps } from "$fresh/server.ts";
import { Button } from "../../components/Button.tsx";
import { MediaHeader } from "../../components/MediaHeader.tsx";
import { baseUrl } from "../../constants.ts";

interface Props {
  message: string | null;
}

export const handler: Handlers<Props> = {
  async GET(req, ctx) {
    return await ctx.render({
      message: null,
    });
  },
  async POST(req, ctx) {
    const form = await req.formData();
    const file = form.get("bookUpload") as File;

    if (!file) {
      return ctx.render({
        message: 'A file is required, please try again.',
      });
    }

    const response = await fetch(`${baseUrl}/books/upload`, {
      method: 'POST',
      body: form,
    });

    if (!response.ok) {
        return ctx.render({
            message: 'File upload failed'
        });
    }

    // on success of upload, redirect to books list page
    return new Response('', {
      status: 303,
      headers: { Location: '/books' }
    });
  },
};

export default function Upload(props: PageProps<Props>) {
  const { message } = props.data;
  return (
    <div>
      <div className="flex items-center justify-between border-b dark:border-slate-300 pb-2">
        <MediaHeader title="Books Upload" />
      </div>
      <div className="mt-2">
        <form method="post" encType="multipart/form-data" className="flex flex-col gap-4 sm:flex-row sm:gap-0 sm:items-center">
          <input type="file" name="bookUpload" />
          <Button type="submit" className="max-w-24 min-w-24">Upload</Button>
        </form>
        {message ? <p>{message}</p> : null}
      </div>
    </div>
  );
}
