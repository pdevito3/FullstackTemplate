namespace FullstackTemplate.AppHost;

internal static class Config
{
    // Set before WithDataVolume, because Postgres 18 uses a different data path. The test projects compile this file,
    // so the tests run the same Postgres version as the app.
    public const string PostgresImageTag = "17.6";

    // The compat image has the AWS CLI, which the bucket hook in Storage/seed needs. The integration tests run the
    // same image, so they prove the presigned URLs against the emulator of the app.
    public const string FlociImageTag = "2.3.0-compat";

    // Floci accepts any signature by default. With this setting it checks each one, as S3 does, so a presigned URL
    // that the server signs wrong fails in a local run and in the tests.
    public const string FlociValidateSignaturesVariable = "FLOCI_AUTH_VALIDATE_SIGNATURES";
}
