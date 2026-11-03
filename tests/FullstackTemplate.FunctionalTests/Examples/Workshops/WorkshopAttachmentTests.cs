namespace FullstackTemplate.FunctionalTests.Examples.Workshops;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.S3.Model;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops;
using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.Fakes;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using TestUtilities;

public class WorkshopAttachmentTests : TestBase
{
    private const string RowRefusal = "Only the organizer of this workshop or a tenant admin can change it.";

    // A client with no token: a presigned URL is the only proof that it needs
    private static readonly HttpClient Storage = new();

    [Test]
    public async Task the_download_url_of_an_uploaded_file_gives_its_bytes_as_an_attachment_with_its_name()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        var bytes = RandomBytes(4096);

        // Act
        var result = await UploadAsync(workshop.Id, bytes, "résumé (final).pdf", "application/pdf");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.Headers.Location.ShouldNotBeNull().ToString().ShouldEndWith($"/workshops/{workshop.Id}/attachments");
        var attachment = (await result.Content.ReadFromJsonAsync<WorkshopAttachmentDto>()).ShouldNotBeNull();
        attachment.FileName.ShouldBe("résumé (final).pdf");
        attachment.ContentType.ShouldBe("application/pdf");
        attachment.SizeBytes.ShouldBe(bytes.Length);
        attachment.PreviewUrl.ShouldBeNull();

        var download = await Storage.GetAsync(attachment.DownloadUrl);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        download.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/pdf");
        download.Content.Headers.ContentDisposition.ShouldNotBeNull().ToString()
            .ShouldBe("attachment; filename*=UTF-8''r%C3%A9sum%C3%A9%20%28final%29.pdf");
    }

    [Test]
    public async Task the_preview_url_of_an_uploaded_image_shows_it_inline()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        var bytes = RandomBytes(512);

        // Act
        var result = await UploadAsync(workshop.Id, bytes, "kiln.png", "image/png");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        var attachment = (await result.Content.ReadFromJsonAsync<WorkshopAttachmentDto>()).ShouldNotBeNull();
        var preview = await Storage.GetAsync(attachment.PreviewUrl.ShouldNotBeNull());
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await preview.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        preview.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("image/png");
        preview.Content.Headers.ContentDisposition.ShouldNotBeNull().DispositionType.ShouldBe("inline");
    }

    [Test]
    public async Task the_list_gives_the_files_of_the_workshop_in_upload_order()
    {
        // Arrange
        var (workshop, other) = await AddTwoWorkshopsAndSignInAsync();
        await UploadAsync(workshop.Id, RandomBytes(10), "first.txt", "text/plain");
        await UploadAsync(workshop.Id, RandomBytes(10), "second.txt", "text/plain");
        await UploadAsync(other.Id, RandomBytes(10), "elsewhere.txt", "text/plain");

        // Act
        var result = await ListAsync(workshop.Id);

        // Assert
        result.Select(a => a.FileName).ShouldBe(["first.txt", "second.txt"]);
    }

    [Test]
    public async Task a_delete_removes_the_file_from_the_list_and_from_storage()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        var upload = await UploadAsync(workshop.Id, RandomBytes(100), "notes.txt", "text/plain");
        var attachment = (await upload.Content.ReadFromJsonAsync<WorkshopAttachmentDto>()).ShouldNotBeNull();

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Workshops.Attachment(workshop.Id, attachment.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(workshop.Id)).ShouldBeEmpty();
        (await Storage.GetAsync(attachment.DownloadUrl)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task a_member_of_another_tenant_cannot_find_the_files_of_a_workshop()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        var upload = await UploadAsync(workshop.Id, RandomBytes(100), "notes.txt", "text/plain");
        var attachment = (await upload.Content.ReadFromJsonAsync<WorkshopAttachmentDto>()).ShouldNotBeNull();
        await AddWorkshopAndSignInAsync("Admin");

        // Act
        var list = await FactoryClient.GetRequestAsync(ApiRoutes.Workshops.Attachments(workshop.Id));
        var add = await UploadAsync(workshop.Id, RandomBytes(10), "intruder.txt", "text/plain");
        var delete = await FactoryClient.DeleteRequestAsync(ApiRoutes.Workshops.Attachment(workshop.Id, attachment.Id));

        // Assert
        list.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        add.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Storage.GetAsync(attachment.DownloadUrl)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task a_file_of_another_workshop_is_not_found_under_this_workshop()
    {
        // Arrange
        var (workshop, other) = await AddTwoWorkshopsAndSignInAsync();
        var upload = await UploadAsync(other.Id, RandomBytes(10), "elsewhere.txt", "text/plain");
        var attachment = (await upload.Content.ReadFromJsonAsync<WorkshopAttachmentDto>()).ShouldNotBeNull();

        // Act
        var result = await FactoryClient.DeleteRequestAsync(ApiRoutes.Workshops.Attachment(workshop.Id, attachment.Id));

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync(other.Id)).ShouldHaveSingleItem().Id.ShouldBe(attachment.Id);
    }

    [Test]
    public async Task a_member_with_manage_workshops_who_is_not_the_organizer_cannot_add_or_delete_files()
    {
        // Arrange
        var (_, otherManager, workshop) = await AddOrganizedWorkshopAsync();
        var upload = await UploadAsync(workshop.Id, RandomBytes(10), "notes.txt", "text/plain");
        var attachment = (await upload.Content.ReadFromJsonAsync<WorkshopAttachmentDto>()).ShouldNotBeNull();
        FactoryClient.SignInAsAppUser(otherManager.Identifier);

        // Act
        var add = await UploadAsync(workshop.Id, RandomBytes(10), "other.txt", "text/plain");
        var delete = await FactoryClient.DeleteRequestAsync(ApiRoutes.Workshops.Attachment(workshop.Id, attachment.Id));

        // Assert
        add.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await add.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull().Detail.ShouldBe(RowRefusal);
        delete.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await delete.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull().Detail.ShouldBe(RowRefusal);
        (await ListAsync(workshop.Id)).ShouldHaveSingleItem().Id.ShouldBe(attachment.Id);
    }

    [Test]
    public async Task a_member_with_no_permission_reads_the_files_but_cannot_add_one()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("User");

        // Act
        var list = await FactoryClient.GetRequestAsync(ApiRoutes.Workshops.Attachments(workshop.Id));
        var add = await UploadAsync(workshop.Id, RandomBytes(10), "notes.txt", "text/plain");

        // Assert
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        add.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task a_file_larger_than_10_mb_is_refused_and_does_not_get_to_storage()
    {
        // Arrange
        var (tenant, workshop) = await AddWorkshopAndSignInAsync("Admin");

        // Act
        var result = await UploadAsync(workshop.Id, RandomBytes(11 * 1024 * 1024), "kiln-video.mp4", "video/mp4");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors["sizeBytes"].ShouldBe(["A file can be no larger than 10 MB. Choose a smaller file."]);
        (await ObjectKeysOfAsync(tenant, workshop)).ShouldBeEmpty();
    }

    [Test]
    public async Task an_empty_file_is_refused()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");

        // Act
        var result = await UploadAsync(workshop.Id, [], "empty.txt", "text/plain");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = (await result.Content.ReadFromJsonAsync<ValidationProblemDetails>()).ShouldNotBeNull();
        problem.Errors["sizeBytes"].ShouldBe(["The file is empty. Choose a file that has content."]);
    }

    [Test]
    public async Task a_failed_save_deletes_the_uploaded_object()
    {
        // Arrange
        var (tenant, workshop) = await AddWorkshopAndSignInAsync("Admin");
        TestingWebApplicationFactory.Instance.FailingSaves
            .ArmWhere(e => e is WorkshopAttachment a && a.WorkshopId == workshop.Id);

        // Act
        var result = await UploadAsync(workshop.Id, RandomBytes(100), "notes.txt", "text/plain");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await ObjectKeysOfAsync(tenant, workshop)).ShouldBeEmpty();
        (await ListAsync(workshop.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task a_save_that_fails_after_its_commit_keeps_the_object_of_the_saved_row()
    {
        // Arrange
        var (_, workshop) = await AddWorkshopAndSignInAsync("Admin");
        TestingWebApplicationFactory.Instance.FailingSaves
            .ArmAfterCommitWhere(e => e is WorkshopAttachment a && a.WorkshopId == workshop.Id);
        var bytes = RandomBytes(100);

        // Act
        var result = await UploadAsync(workshop.Id, bytes, "notes.txt", "text/plain");

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var saved = (await ListAsync(workshop.Id)).ShouldHaveSingleItem();
        var download = await Storage.GetAsync(saved.DownloadUrl);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    private async Task<HttpResponseMessage> UploadAsync(string workshopId, byte[] bytes, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        return await FactoryClient.PostAsync(ApiRoutes.Workshops.Attachments(workshopId), form);
    }

    private async Task<List<WorkshopAttachmentDto>> ListAsync(string workshopId)
    {
        var result = await FactoryClient.GetRequestAsync(ApiRoutes.Workshops.Attachments(workshopId));
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await result.Content.ReadFromJsonAsync<List<WorkshopAttachmentDto>>()).ShouldNotBeNull();
    }

    private static async Task<List<string>> ObjectKeysOfAsync(Tenant tenant, Workshop workshop)
    {
        var storage = TestingWebApplicationFactory.Instance.Storage;
        using var client = storage.CreateClient();
        var page = await client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = storage.BucketName,
            Prefix = $"tenants/{tenant.Id}/workshops/{workshop.Id}/",
        });
        return page.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private async Task<(Tenant Tenant, Workshop Workshop)> AddWorkshopAndSignInAsync(string role)
    {
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = role)
            .Under(tenant).AddVenue(out _).AddWorkshop(out var workshop)
            .BuildAsync();
        FactoryClient.SignInAsAppUser(user.Identifier);
        return (tenant, workshop);
    }

    private async Task<(Workshop Workshop, Workshop Other)> AddTwoWorkshopsAndSignInAsync()
    {
        await StartGraph()
            .AddTenant(out var tenant)
            .AddUser(out var user, u => u.Role = "Admin")
            .Under(tenant).AddVenue(out var venue).AddWorkshop(out var workshop)
            .Under(venue).AddWorkshop(out var other)
            .BuildAsync();
        FactoryClient.SignInAsAppUser(user.Identifier);
        return (workshop, other);
    }

    private async Task<(User Organizer, User OtherManager, WorkshopDto Workshop)> AddOrganizedWorkshopAsync()
    {
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "User", u => u.Role = "User")
            .Under(tenant).AddVenue(out var venue);
        users[0].AddPermission(Permission.ManageWorkshops());
        users[1].AddPermission(Permission.ManageWorkshops());
        await InsertAsync(tenant);
        await InsertAsync(users);
        await InsertAsync(venue);

        FactoryClient.SignInAsAppUser(users[0].Identifier);
        var result = await FactoryClient.PostJsonRequestAsync(ApiRoutes.Workshops.Create,
            new FakeWorkshopForCreationDto(venue.Id).Generate());
        result.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (users[0], users[1], (await result.Content.ReadFromJsonAsync<WorkshopDto>()).ShouldNotBeNull());
    }
}
