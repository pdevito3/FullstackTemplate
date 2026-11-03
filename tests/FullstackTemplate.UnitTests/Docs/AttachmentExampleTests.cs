namespace FullstackTemplate.UnitTests.Docs;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.EntityConfigurations;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.Attachments.Dtos;
using FullstackTemplate.Server.Domain.Attachments.Mappings;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints.Http;
using FullstackTemplate.Server.Resources.Storage;
using FullstackTemplate.SharedTestHelpers.Fakes.Attachments;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shouldly;

// The example of docs/backend/attachments.md: the photos of a repair. The Server does not have it.

public sealed class Repair : BaseEntity, ITenantable
{
    public const string IdPrefix = "rep";

    private Repair() : base(IdPrefix) { }

    public string TenantId { get; private set; } = default!;

    public static Repair Create(string tenantId) => new() { TenantId = tenantId };
}

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

    private RepairPhoto() : base(IdPrefix) { } // For EF Core
}

public sealed class RepairPhotoConfiguration : IEntityTypeConfiguration<RepairPhoto>
{
    public void Configure(EntityTypeBuilder<RepairPhoto> builder)
    {
        builder.HasKey(e => e.Id);

        // A photo is its own root, so an upload does not change the version of the repair
        builder.HasOne<Repair>()
            .WithMany()
            .HasForeignKey(e => e.RepairId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.ComplexProperty(e => e.File, file => file.MapStoredFile());

        builder.HasIndex(e => new { e.RepairId, e.UploadedAt, e.Id });
    }
}

public static class AddRepairPhoto
{
    /// <param name="Content">The bytes of the file, in a stream that can seek. The entrypoint owns the stream.</param>
    public sealed record Command(string RepairId, Stream Content, string FileName, string? MediaType, long SizeBytes)
        : IRequest<AttachmentDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        AttachmentStorage attachments,
        ObjectStorage storage,
        TimeProvider timeProvider)
        : IRequestHandler<Command, AttachmentDto>
    {
        public async Task<AttachmentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // An app checks the permission first, then loads the repair and checks the access of the caller to it
            var repair = await dbContext.Set<Repair>()
                .TagWithOperationCallSite("repairs.photos.add.load_repair")
                .AsNoTracking()
                .GetById(request.RepairId, cancellationToken);

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
        }
    }
}

public sealed class AddRepairPhotoEndpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
        .MapPost("/repairs/{id}/photos", Handle)
        .WithName("AddRepairPhoto")
        .WithTags("Repairs")
        .MapToApiVersion(ApiVersions.V1)
        .RequireAuthorization()
        // The API takes a bearer token, not a cookie, so a form post of another site cannot send it
        .DisableAntiforgery()
        .ProducesValidationProblem()
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

    /// <summary>
    /// Uploads one photo to a Repair as a multipart form with the field file. The uploadRules of the list give the allowed types and the largest size.
    /// </summary>
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
}

public static class GetRepairPhotos
{
    public sealed record Query(string RepairId) : IRequest<AttachmentListDto>;

    public sealed class Handler(AppDbContext dbContext, ObjectStorage storage, TimeProvider timeProvider)
        : IRequestHandler<Query, AttachmentListDto>
    {
        public async Task<AttachmentListDto> Handle(Query request, CancellationToken cancellationToken)
        {
            // The tenant filter gives 404 for a repair of another tenant, so no URL of its photos is signed
            var repair = await dbContext.Set<Repair>()
                .TagWithOperationCallSite("repairs.photos.list.load_repair")
                .AsNoTracking()
                .GetById(request.RepairId, cancellationToken);

            var photos = await dbContext.Set<RepairPhoto>()
                .TagWithOperationCallSite("repairs.photos.list")
                .AsNoTracking()
                .Where(p => p.RepairId == repair.Id)
                .OrderBy(p => p.UploadedAt)
                .ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);

            return await photos.ToAttachmentListDtoAsync(RepairPhoto.UploadRules, storage, timeProvider.GetUtcNow());
        }
    }
}

public static class DeleteRepairPhoto
{
    public sealed record Command(string RepairId, string PhotoId) : IRequest;

    public sealed class Handler(AppDbContext dbContext, AttachmentStorage attachments) : IRequestHandler<Command>
    {
        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            // An app checks the permission first, then loads the repair and checks the access of the caller to it
            // A photo of another repair is not found here, so the route cannot delete it
            var photo = await dbContext.Set<RepairPhoto>()
                .TagWithOperationCallSite("repairs.photos.delete.load")
                .Where(p => p.RepairId == request.RepairId)
                .GetById(request.PhotoId, cancellationToken);

            await attachments.DeleteAsync(photo, cancellationToken);
        }
    }
}

public class AttachmentExampleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void a_repair_photo_keeps_the_type_of_its_bytes_under_a_key_of_its_tenant_and_its_repair()
    {
        // Arrange
        var repair = Repair.Create("ten_01a1a0cea6667e4e9cf16ffa20ed5034");

        // Act
        var photo = RepairPhoto.Create(repair, SampleFiles.Upload("front door.webp", SampleFiles.WebP), Now);

        // Assert
        photo.File.MediaType.ShouldBe("image/webp");
        photo.File.StorageKey.ShouldBe($"tenants/{repair.TenantId}/repairs/{repair.Id}/photos/{photo.Id}");
        photo.File.PreviewAt(Now).ShouldNotBeNull();
    }

    [Test]
    public void a_repair_photo_refuses_a_pdf_with_the_types_of_its_rules()
    {
        // Arrange
        var repair = Repair.Create("ten_01a1a0cea6667e4e9cf16ffa20ed5034");

        // Act
        var create = () => RepairPhoto.Create(repair, SampleFiles.Upload("invoice.pdf", SampleFiles.Pdf), Now);

        // Assert
        create.ShouldThrow<ValidationException>().Message.ShouldBe(RepairPhoto.UploadRules.TypeNotAllowedMessage);
    }

    [Test]
    public void the_configuration_maps_the_stored_file_to_its_four_columns()
    {
        // Arrange
        using var context = new RepairContext();

        // Act
        var file = context.Model.FindEntityType(typeof(RepairPhoto))!.FindComplexProperty(nameof(RepairPhoto.File))!;

        // Assert
        file.ComplexType.GetProperties().Select(p => p.GetColumnName())
            .ShouldBe(["file_name", "media_type", "size_bytes", "storage_key"], ignoreOrder: true);
    }

    private sealed class RepairContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Repair>().HasKey(e => e.Id);
            modelBuilder.ApplyConfiguration(new RepairPhotoConfiguration());
        }
    }
}
