namespace FullstackTemplate.Server.Domain.Invites;

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// The secret of an invite link. The server stores only <see cref="Hash"/>, so a read of the database or the audit log
/// cannot claim an invite (INV-2).
/// </summary>
public sealed class InviteToken
{
    private const int ByteCount = 32;

    /// <summary>The length of a token from <see cref="New"/>: 32 bytes in base64url with no padding.</summary>
    public const int Length = 43;

    private InviteToken(string value) => Value = value;

    public string Value { get; }

    public string Hash => HashOf(Value);

    public static InviteToken New() => new(Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(ByteCount)));

    /// <summary>A token that a test or a caller already has. It is not checked, because a wrong token matches no hash.</summary>
    public static InviteToken Of(string value) => new(value);

    public static string HashOf(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public override string ToString() => "***";
}
