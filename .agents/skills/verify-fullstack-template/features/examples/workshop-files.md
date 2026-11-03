# Workshop files

The detail page of a workshop has a Files section. A user who can change the workshop uploads a file, views an image in full, downloads a file, and deletes a file. The bytes go to object storage (Floci in the stack) and the metadata to Postgres. The browser reads each file from storage with a presigned URL. An image (PNG, JPEG, GIF, or WebP) shows a thumbnail. Each other type shows a file icon.

## Sub-features

- `files-empty` shows `This workshop has no files.` in the region `Files` of a new workshop.
- `files-upload` sends one file as `multipart/form-data` to `POST /api/v1/workshops/{id}/attachments`, shows the toast `File uploaded`, and lists the file with its size and upload date.
- `files-thumbnail` shows an image as a thumbnail that loads from the storage origin, with an `X-Amz-Signature` and an `inline` disposition in its URL.
- `files-view` opens the full image in the dialog named for the file. Escape closes it and puts the focus back on `View <file name>`.
- `files-download` saves the file under its name with the bytes that were uploaded.
- `files-too-large` refuses a file over 10 MB with the 422 toast `A file can be no larger than 10 MB. Choose a smaller file.`
- `files-over-the-body-limit` refuses a file over 30 MB, the request limit of Kestrel, with the 413 toast `The file is too large to send. Choose a smaller file.` The BFF writes that problem.
- `files-delete` asks `Delete <file name>?`, sends `DELETE /api/v1/workshops/{id}/attachments/{attachmentId}`, shows the toast `File deleted`, and moves the focus to the heading `Files`. Storage then gives 404 for the old download URL.
- `files-reader` shows the files and their `Download` links to a member who cannot change the workshop, with no `Upload file` and no `Delete`.

## How to get to it (user POV)

- Examples, Workshops in the sidebar, then the title link of a workshop. The region `Files` is under the actions.
- In the region `Files`: the button `Upload file`, and for each row of the list `Files` the buttons `View <file name>` (images only) and `Delete <file name>`, and the link `Download <file name>`.

## Driving it with browse.mjs

Preconditions:

- `doctor.sh` exits with 0, and the `storage` resource is `Healthy`.
- The database is empty, so the recipe starts a tenant as in [Workshops](./workshops.md). The user is the `Admin` of the tenant, so `allowedActions` of the workshop has `ManageAttachments`.

- **Setup.** `POST /api/v1/venues` and `POST /api/v1/workshops` with `fetch` and the `X-CSRF: 1` header. The forms are the subject of [Workshops](./workshops.md).
- **Empty.** Open `/workshops/<id>`. `GET /api/v1/workshops/<id>/attachments` gives 200 with `[]`, and the empty text shows.
- **Upload an image.** Click `Upload file`, and give the file chooser a PNG with `fileChooser.setFiles({ name, mimeType: 'image/png', buffer })`. The `POST` gives 201 with `contentType` `image/png` and the size of the file. The thumbnail in `View <file name>` has a `naturalWidth` above 0, and its `src` is on another origin than the app.
- **Upload another file.** Upload a PDF. The `POST` gives 201 with `previewUrl` null. The row has no `View` button, and `Download <file name>` has the `downloadUrl` of the answer as its `href`.
- **Too large.** Upload a file of 11 MB. The `POST` gives 422, and the toast shows its `detail`.
- **Over the body limit.** Upload a file of 31 MB. The `POST` gives 413, and the toast shows `The file is too large to send. Choose a smaller file.` Do not read the body of that response in the recipe: Chromium does not keep it.
- **View.** Click `View <file name>`. The dialog shows the image with a `naturalWidth` above 0. Press Escape.
- **Download.** Click `Download <file name>` and wait for the `download` event. `suggestedFilename()` is the uploaded name, and the bytes at `download.path()` are the uploaded bytes.
- **Delete.** Click `Delete <file name>`, then `Delete file` in the dialog. The `DELETE` gives 204, the row goes, and `page.request.get(downloadUrl)` gives 404.
- **Persist.** Reload. The list has the rows that the `GET` gives.
- **Proof.** Run `node $S/scripts/browse.mjs $S/recipes/examples/workshop-files.mjs`. The captures `files-empty`, `two-files`, `too-large`, `image-dialog`, `delete-dialog`, and `after-reload` show the states.

## Gotchas

- The restore of `browse.mjs` puts back the database rows, but not the objects in storage. The recipe deletes each file that it uploads, then the tenant, also after a failed step.
- The presigned URLs expire after 15 minutes. The page loads the list again a minute before, so a URL that a recipe copied from an old answer can stop working.
- The `Download` link goes to the storage origin. The `Content-Disposition` of the signed URL makes the browser save the file, so the page does not change.
- Floci does not refuse a request with no signature. Do not take a 200 for an unsigned URL as a fault of the app.
- With `--sweep`, the file names on the screen are long values, but storage still saves a download under the uploaded name.
