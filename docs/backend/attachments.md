# Attachments

An attachment is a file that a record keeps, such as the photo of a repair or the PDF of a contract. The bytes go to object storage. The row of the attachment keeps the name, the type, the size, and the key of the object. The parts that each use shares are in `FullstackTemplate.Server/Domain/Attachments/` and `frontend/src/components/attachments/`. Each entity that takes files adds its own attachment entity, its upload rules, its endpoints, and a Files section on its page.

For the bucket in each environment, see [Object storage](../backend.md#object-storage).

## When to use attachments

- Use an attachment for a file that a user uploads and that the app gives back as it is: a photo, a scan, or a PDF.
- For a file that the app reads, such as a CSV import, read the content in the request. An attachment keeps bytes that the app does not read.
- For a file of the app itself, such as a logo, put it in the SPA.
- Each attachment belongs to one record of one tenant.

## The parts

| Part | What it does |
|------|--------------|
| [`FileType`](../../FullstackTemplate.Server/Domain/Attachments/FileType.cs) | The registry of the types that an upload can have: the name, the media type, the extensions, the first bytes, and whether the page can show it |
| [`UploadRules`](../../FullstackTemplate.Server/Domain/Attachments/UploadRules.cs) | The allowed types and the largest size of one use. It checks an upload and writes the messages from its own limits |
| [`FileUpload`](../../FullstackTemplate.Server/Domain/Attachments/FileUpload.cs) | What the client sent: the name, the claimed media type, the size, and the first bytes |
| [`StoredFile`](../../FullstackTemplate.Server/Domain/Attachments/StoredFile.cs) | The value object of an attachment entity. `Accept` checks the upload. `DownloadAt` and `PreviewAt` give the presigned GETs |
| [`IAttachment`](../../FullstackTemplate.Server/Domain/Attachments/IAttachment.cs) | The interface of each attachment entity |
| [`AttachmentStorage`](../../FullstackTemplate.Server/Domain/Attachments/AttachmentStorage.cs) | The add and the delete, in the order of [The order of the writes](#the-order-of-the-writes) |
| [`AttachmentMapper`](../../FullstackTemplate.Server/Domain/Attachments/Mappings/AttachmentMapper.cs) | The `AttachmentDto` with signed URLs, and the `AttachmentListDto` with the upload rules |
| [`MapStoredFile`](../../FullstackTemplate.Server/Databases/EntityConfigurations/StoredFileMapping.cs) | The columns `file_name`, `media_type`, `size_bytes`, and `storage_key` |
| [`AttachmentsSection`](../../frontend/src/components/attachments/attachments-section.tsx) | The Files section of a page: the heading, the hint, the upload button, the list, and the delete |
| [`refreshBeforeUrlsExpire`](../../frontend/src/lib/attachment-list-refresh.ts) | The query options that load the list again before its URLs expire |

## Add attachments to an entity

The steps use a `Repair` that keeps photos. The code is in [AttachmentExampleTests.cs](../../tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs). Change the names.

### On the Server

1. **The entity.** Add `Domain/<Owner>s/<Owner><Noun>.cs`, such as `Domain/Repairs/RepairPhoto.cs`, as a tenant-owned root that implements `IAttachment`. Declare its upload rules, and make the key from IDs only. Done when a unit test shows the key and a refused type.

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   public sealed class RepairPhoto : BaseEntity, ITenantable, IAttachment
   {
       public const string IdPrefix = "repph";

       /// <summary>The photos of a repair: images that show in the page, up to 5 MB.</summary>
       public static readonly UploadRules UploadRules = new(5, FileType.Png, FileType.Jpeg, FileType.WebP);

       [NotSensitive] public string TenantId { get; private set; } = default!;
       [NotSensitive] public string RepairId { get; private set; } = default!;
       // A file name can name a person, such as "Jane Doe front door.jpg"
       [Sensitive] public StoredFile File { get; private set; } = default!;
       [NotSensitive] public DateTimeOffset UploadedAt { get; private set; }

       public static RepairPhoto Create(Repair repair, FileUpload upload, DateTimeOffset uploadedAt)
       {
           var photo = new RepairPhoto
           {
               TenantId = repair.TenantId,
               RepairId = repair.Id,
               UploadedAt = uploadedAt.ToUniversalTime(),
           };
           // The key starts with the tenant, so that the files of each tenant stay apart
           photo.File = StoredFile.Accept(upload, UploadRules,
               $"tenants/{photo.TenantId}/repairs/{photo.RepairId}/photos/{photo.Id}");
           return photo;
       }
       // ...
   }
   ```

2. **The configuration and the migration.** Map the tenant and the owner as each tenant-owned root does ([entity-configurations.md](entity-configurations.md)). Map the file with `MapStoredFile`, and index the order of the list. Add the `DbSet` and a migration. Done when `ModelSnapshotTests` passes.

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   // A photo is its own root, so an upload does not change the version of the repair
   builder.HasOne<Repair>()
       .WithMany()
       .HasForeignKey(e => e.RepairId)
       .OnDelete(DeleteBehavior.ClientNoAction);

   builder.ComplexProperty(e => e.File, file => file.MapStoredFile());

   builder.HasIndex(e => new { e.RepairId, e.UploadedAt, e.Id });
   ```

3. **The action.** Add a `Manage<Noun>s` action, such as `ManagePhotos`, to the action type of the owner ([domain-entities.md](domain-entities.md#allowed-actions)). Make it apply in each status of the owner, because a file stays useful after the record closes. The table of actions fills `allowedActions` of the read DTO of the owner, so the SPA shows the upload and the delete only to a caller who can send them. Done when a unit test shows the action in the last status of the owner, and none for a caller with no permission.

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   // The photos of a repair stay useful after it closes, so the action applies in each status
   public static readonly RepairActionEnum ManagePhotos = new AnyStatusType(nameof(ManagePhotos), 1);
   // ...
   private sealed class AnyStatusType(string name, int value) : RepairActionEnum(name, value)
   {
       public override bool AppliesTo(Repair repair) => true;
   }
   ```

4. **The add.** The command takes a stream, not an `IFormFile`. Call `GuardCaller` of the action before the load of the owner, and the access check of the owner, such as `GuardChangeBy`, after it. Then read the first bytes and make the entity. Give the save of the row to `AttachmentStorage.AddAsync`. The save takes the shared lock of the tenant, as each add of a tenant-owned root does ([concurrency.md](concurrency.md#lock)).

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   var caller = await permissionGuard.GetCallerAsync(cancellationToken);
   RepairAction.ManagePhotos().GuardCaller(caller);
   // ...
   repair.GuardChangeBy(caller);

   // The rules run before the upload, so a file that they refuse never gets to storage
   var upload = await FileUpload.ReadAsync(request.Content, request.FileName, request.MediaType,
       request.SizeBytes, cancellationToken);
   var photo = RepairPhoto.Create(repair, upload, timeProvider.GetUtcNow());

   // The upload is before the lock, because the work of the lock can run more than one time
   await attachments.AddAsync(photo, request.Content, async () =>
       await RootLock.Tenant.RunSharedAsync(dbContext, repair.TenantId, async ct =>
       {
           await dbContext.Set<Repair>()
               .TagWithOperationCallSite("repairs.photos.add.check_repair")
               .AsNoTracking()
               .GetById(repair.Id, ct);

           await dbContext.Set<RepairPhoto>().AddAsync(photo, ct);
           await dbContext.SaveChangesAsync(ct);
           return photo;
       }, cancellationToken), cancellationToken);

   return await photo.ToAttachmentDtoAsync(storage, timeProvider.GetUtcNow());
   ```

5. **The upload endpoint.** Only the endpoint knows `IFormFile` (`EntrypointBoundaryTests`). It opens the stream and owns it. Kubb then writes a client that takes `body: { file }`.

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   // The API takes a bearer token, not a cookie, so a form post of another site cannot send it
   .DisableAntiforgery()
   // ...
   public static async Task<CreatedAtRoute<AttachmentDto>> Handle(
       string id,
       IFormFile file,
       IMediator mediator,
       CancellationToken cancellationToken)
   {
       await using var content = file.OpenReadStream();
       var result = await mediator.Send(
           new AddRepairPhoto.Command(id, content, file.FileName, file.ContentType, file.Length),
           cancellationToken);
       return TypedResults.CreatedAtRoute(result, "GetRepairPhotos", new { id });
   }
   ```

6. **The list.** Load the owner through the tenant filter, then its attachments. The list response gives the upload rules of the entity, so the SPA copies no limit.

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   return await photos.ToAttachmentListDtoAsync(RepairPhoto.UploadRules, storage, timeProvider.GetUtcNow());
   ```

7. **The delete.** Guard the action and the owner as the add does. Load the attachment under its owner, so that a route with another owner finds nothing. Then call `DeleteAsync`.

   <!-- code: tests/FullstackTemplate.UnitTests/Docs/AttachmentExampleTests.cs -->
   ```csharp
   RepairAction.ManagePhotos().GuardCaller(caller);

   var repair = await dbContext.Set<Repair>()
       .TagWithOperationCallSite("repairs.photos.delete.load_repair")
       .AsNoTracking()
       .GetById(request.RepairId, cancellationToken);
   repair.GuardChangeBy(caller);

   // A photo of another repair is not found here, so the route cannot delete it
   var photo = await dbContext.Set<RepairPhoto>()
       .TagWithOperationCallSite("repairs.photos.delete.load")
       .Where(p => p.RepairId == repair.Id)
       .GetById(request.PhotoId, cancellationToken);

   await attachments.DeleteAsync(photo, cancellationToken);
   ```

8. **The tests.** Give the entity graph a blueprint that makes the upload with `SampleFiles` of `SharedTestHelpers/Fakes/Attachments/`. Add the entity to `TenantDeleteOwnedRowsTests`, each endpoint to `EndpointAccessSweepTests`, and the columns to `StringColumnLengthTests`: `file_name` goes in `CallerColumns`, and `media_type` and `storage_key` go in `ServerWrittenColumns`. The sweep sends a file of each type of the registry until the rules of the route take one, so it needs no copy of the rules. Write functional tests for an upload, a type that the rules refuse, a file whose bytes do not match its name, a file over the largest size, and the rules in the list. Done when `dotnet test --solution FullstackTemplate.slnx` passes.
9. **The contract.** Regenerate it with the `api-contract` skill.

### In the SPA

1. **The hooks.** Write the list query with `refreshBeforeUrlsExpire`. The upload and the delete invalidate the list. The delete invalidates it also after a refusal, because another user can have deleted the file.

   ```ts
   export const repairPhotosQueryOptions = (repairId: string) =>
     queryOptions({
       queryKey: RepairKeys.photos(repairId),
       queryFn: ({ signal }) => getRepairPhotos({ path: { id: repairId }, signal }),
       ...refreshBeforeUrlsExpire,
     })
   ```

2. **The section.** Give `AttachmentsSection` the three hooks. It shows the hint, such as "PNG, JPEG, or WebP. Up to 5 MB.", and it lets the file chooser offer only the types of the rules. It shows the toasts and moves the focus after a delete.

   ```tsx
   <AttachmentsSection
     list={photos}
     upload={add}
     remove={remove}
     canManage={repair.allowedActions.includes('ManagePhotos')}
     emptyText="This repair has no photos."
   />
   ```

3. **The tests.** In the mock of the list, give upload rules that differ from the rules of the Server, so that a test shows that the SPA reads them. Done when `pnpm test:e2e` shows the hint, a refused type, and the toast of the message of the Server.

The components declare the fields that they read in `components/attachments/types.ts`, because Kubb writes a DTO type only when an endpoint returns it. The generated `AttachmentListDto` of a use must fit them, so a change to the DTO fails the build at the page that passes it.

## The two byte paths

- **Up, through the Server.** The SPA posts the file as a multipart form to the BFF, and the BFF forwards it. The Server keeps a form file over 64 KB in a temporary file, not in memory. The handler reads only the first `FileType.HeadLength` bytes, and puts the stream back at its start. The put then sends the whole file to storage from the same stream.
- **Down, from storage.** The browser reads the bytes with a presigned GET URL. The Server has no endpoint that sends the bytes of a stored file.

## The type and size rules

Each attachment entity declares its own `UploadRules`: the largest size in MB and the allowed types. `StoredFile.Accept` refuses an upload with a `ValidationException` in this order:

1. The file is empty, or larger than the largest size (`sizeBytes`).
2. Its first bytes are not a type of the rules (`mediaType`). The name and the claim of the client do not change this.
3. The extension of the name is not an extension of that type (`fileName`). A PNG with the name `invoice.pdf` is refused.
4. The client claimed another media type (`mediaType`). No type, and `application/octet-stream`, are no claim.

The stored media type is the type of the bytes, not the claim of the client. A download sends that type.

The messages come from the rules: `FileTooLargeMessage` and `TypeNotAllowedMessage` name the limits of the use. Keep each limit of a use only in its rules. A test reads the limit and the message from the rules, such as `UploadRules.MaxSizeBytes + 1`. The SPA builds its hint and its `accept` list from the rules of the list response ([server-state.md](../frontend/server-state.md#rules), rule 8). The `accept` list only helps the user. The Server decides.

Kestrel of the BFF and of the Server refuses a request body over 30,000,000 bytes before the rules run. The BFF then gives the 413 the problem detail `RequestTooLargeProblem.Detail`. So the constructor of `UploadRules` refuses a largest size over `UploadRules.LargestMaxSizeMegabytes`. A file of that size and its form fit in the limit, so the user gets the message of the rules. `UploadRequestLimitTests` fails when the constant grows past the request limit of the Server or the BFF.

To add a type to the registry, give it the fixed first bytes that each of its files starts with. Do not add a type that has no such bytes, such as CSV or SVG. Do not add a type whose bytes another type also starts with: a DOCX and an XLSX file both start with the bytes of a ZIP file. Set `canPreview` only for a raster image. `FileTypeTests` checks each signature with the samples of `SampleFiles`, so add a sample from the specification of the format, and give it to `SampleFiles.Of`.

## Presigned URLs

1. Sign a URL only after the handler loads the record through the tenant filter and passes its access check. Anyone who has the URL can read the object until the URL expires.
2. A URL works for `StoredFile.UrlLifetime`, 15 minutes. The list gives `urlsExpireAt` of each file, and `refreshBeforeUrlsExpire` loads the list again a minute before the first URL expires.
3. Put the type and the disposition in the signed URL. `DownloadAt` signs `PresignedGet.Attachment`, so the browser saves the file under its name. `PreviewAt` signs `PresignedGet.Inline` only for a type that the page can show, and gives null for each other type. The page does not show SVG or HTML, because a browser runs their scripts.

A presigned URL works only while the credentials that signed it are valid. When they expire, S3 refuses the URL with `ExpiredToken`, also before `urlsExpireAt`. The credentials of a role are temporary: an ECS task, an EC2 instance profile, IRSA on EKS, and AWS SSO all give credentials that last from about one hour to some hours, and the SDK then gets new ones. So a URL that the Server signs shortly before the old credentials expire stops early. See [the S3 docs on presigned URLs](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html). In production, do these things:

- Keep the life of a URL much shorter than the life of the credentials. The 15 minutes of `StoredFile.UrlLifetime` are short next to a role session of an hour or more. Then few URLs stop early, and the next load of the list signs new URLs with the new credentials.
- If a URL must not stop early, do not sign it with session credentials. Serve the bucket through CloudFront, and sign CloudFront URLs with the key pair of a key group. Do not give the Server long-lived access keys only to make URLs last longer.

## The order of the writes

A row with no object is a file that no one can download. An object with no row costs only its storage. So `AttachmentStorage` writes in this order:

- **Add.** It puts the object, then runs the save of the row. If the save throws, it deletes the object only when no row with its ID exists, because a save can commit and still throw, for example after a lost reply of the commit. If that check fails, it keeps the object.
- **Delete.** It saves the delete of the row, then deletes the object. A failure after the commit leaves an object that no row names, and it writes an error log for it.

Give the save to `AddAsync` as a function, so that the put runs one time, also when the lock runs its work again.

## Tenant isolation

- Make the key from IDs only, and start it with `tenants/{tenantId}/`. Do not put a file name in a key, because the user types it.
- Load the owner through the tenant filter before a sign, an add, or a delete. A record of another tenant then gives 404.
- The add takes the shared lock of the tenant, so that a row does not land in a tenant that a delete removes.

## Limits

- A tenant delete soft-deletes the rows of the files, and keeps their objects ([#413](https://github.com/pdevito3/FullstackTemplate/issues/413)).
- An object whose delete failed after its row delete stays. The Server logs it, and nothing deletes it later ([#413](https://github.com/pdevito3/FullstackTemplate/issues/413)).
- A URL stops before `urlsExpireAt` when the credentials that signed it expire. See [Presigned URLs](#presigned-urls).

<!-- example:start -->
## The example

The workshop example stores the files of a workshop: [`WorkshopAttachment`](<../../FullstackTemplate.Server/Examples/Workshops/WorkshopAttachment.cs>), [`AddWorkshopAttachment`](<../../FullstackTemplate.Server/Examples/Workshops/Features/AddWorkshopAttachment/AddWorkshopAttachment.cs>), [`GetWorkshopAttachments`](<../../FullstackTemplate.Server/Examples/Workshops/Features/GetWorkshopAttachments/GetWorkshopAttachments.cs>), and [`DeleteWorkshopAttachment`](<../../FullstackTemplate.Server/Examples/Workshops/Features/DeleteWorkshopAttachment/DeleteWorkshopAttachment.cs>). Its page wires `AttachmentsSection` in [`workshop-attachments.tsx`](<../../frontend/src/examples/workshops/components/workshop-attachments.tsx>). Its tests are in [`WorkshopAttachmentTests`](<../../tests/FullstackTemplate.FunctionalTests/Examples/Workshops/WorkshopAttachmentTests.cs>) and [`workshop-files.spec.ts`](<../../frontend/tests/features/examples/workshops/workshop-files.spec.ts>).
<!-- example:end -->
