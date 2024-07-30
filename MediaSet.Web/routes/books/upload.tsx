import { Handlers, type PageProps } from "$fresh/server.ts";
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
        message: `A file is required, please try again.`,
      });
    }
    console.log("attempting to upload file", file);

    const response = await fetch(`${baseUrl}/books/upload`, {
      method: "POST",
      body: form,
    });

    if (!response.ok) {
        return ctx.render({
            message: "File upload failed"
        });
    }

    console.log("received upload response", response);
    const name = file.name;
    // const contents = await file.text();
    // console.log(contents);

    return ctx.render({
      message: `${name} uploaded!`,
    });
  },
};

export default function Upload(props: PageProps<Props>) {
  const { message } = props.data;
  return (
    <div>
      <div className="flex items-center justify-between">
        <MediaHeader title="Books Upload" />
      </div>
      <div className="mt-2">
        <form method="post" encType="multipart/form-data">
          <input type="file" name="bookUpload" />
          <button type="submit">Upload</button>
        </form>
        {message ? <p>{message}</p> : null}
      </div>
    </div>
  );
}
