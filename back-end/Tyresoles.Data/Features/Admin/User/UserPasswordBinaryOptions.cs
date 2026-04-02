namespace Tyresoles.Data.Features.Admin.User;

/// <summary>
/// Configures whether [Salt] and [Encrypted Password] are sent as binary (IMAGE) or string (NVARCHAR).
/// Set to true only when the database columns are IMAGE/VARBINARY; default is false (NVARCHAR).
/// </summary>
public class UserPasswordBinaryOptions
{
    public const string SectionName = "UserPasswordBinary";

    /// <summary>
    /// When true, Salt and Encrypted Password are converted to binary before UPDATE (for IMAGE/VARBINARY columns).
    /// When false (default), they are sent as strings (for NVARCHAR columns).
    /// </summary>
    public bool ConvertPasswordColumnsToBinary { get; set; }
}
