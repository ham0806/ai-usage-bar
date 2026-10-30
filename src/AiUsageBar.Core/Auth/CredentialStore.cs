using System.Runtime.InteropServices;
using AiUsageBar.Core.Win32;

namespace AiUsageBar.Core.Auth;

public static class CredentialStore
{
    public const string Service = "ai-usage-bar";
    public const string CursorTokenUser = CredentialUsers.Cursor;

    public static string? GetCursorToken() => GetToken(CursorTokenUser);

    public static void SetCursorToken(string? token) => SetToken(CursorTokenUser, token);

    public static string? GetToken(string user)
    {
        try
        {
            var value = GetPassword(Service, user);
            var text = value?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch (Exception)
        {
            AppLog.Warning($"Credential Manager から {user} のトークンを読めませんでした");
            return null;
        }
    }

    public static void SetToken(string user, string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                DeletePassword(Service, user);
                return;
            }

            SetPassword(Service, user, token.Trim());
        }
        catch (Exception)
        {
            AppLog.Warning($"Credential Manager へ {user} のトークンを保存できませんでした");
        }
    }

    public static string AsSafeError(Exception exc)
    {
        var text = string.IsNullOrWhiteSpace(exc.Message) ? exc.GetType().Name : exc.Message.Trim();
        var lowered = text.ToLowerInvariant();
        foreach (var secret in new[] { "bearer ", "cookie", "eyj", "workoscursor" })
        {
            if (lowered.Contains(secret, StringComparison.Ordinal))
            {
                return exc.GetType().Name;
            }
        }

        return text.Length > 180 ? text[..177] + "..." : text;
    }

    private static string CompoundName(string username, string service) => $"{username}@{service}";

    private static string? GetPassword(string service, string username)
    {
        var cred = ReadCredential(service);
        if (cred is null || (!string.IsNullOrEmpty(username) && !string.Equals(cred.Value.UserName, username, StringComparison.Ordinal)))
        {
            cred = ReadCredential(CompoundName(username, service));
        }

        return cred?.Value;
    }

    private static void SetPassword(string service, string username, string password)
    {
        var existing = ReadCredential(service);
        if (existing is not null)
        {
            var existingUsername = existing.Value.UserName ?? "";
            WriteCredential(CompoundName(existingUsername, service), existingUsername, existing.Value.Value);
        }

        WriteCredential(service, username, password);
    }

    private static void DeletePassword(string service, string username)
    {
        foreach (var target in new[] { service, CompoundName(username, service) })
        {
            var existing = ReadCredential(target);
            if (existing is not null && string.Equals(existing.Value.UserName, username, StringComparison.Ordinal))
            {
                NativeMethods.CredDeleteW(target, NativeMethods.CredTypeGeneric, 0);
            }
        }
    }

    private static CredentialRead? ReadCredential(string target)
    {
        if (!NativeMethods.CredReadW(target, NativeMethods.CredTypeGeneric, 0, out var ptr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NativeMethods.ErrorNotFound)
            {
                return null;
            }

            throw new InvalidOperationException($"CredRead failed: {error}");
        }

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(ptr);
            var blob = cred.CredentialBlobSize == 0
                ? ""
                : Marshal.PtrToStringUni(cred.CredentialBlob, cred.CredentialBlobSize / 2) ?? "";
            return new CredentialRead(cred.UserName ?? "", blob);
        }
        finally
        {
            NativeMethods.CredFree(ptr);
        }
    }

    private static void WriteCredential(string target, string username, string password)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(password);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new CREDENTIAL
            {
                Type = NativeMethods.CredTypeGeneric,
                TargetName = target,
                UserName = username,
                CredentialBlob = blob,
                CredentialBlobSize = bytes.Length,
                Comment = "Stored using python-keyring",
                Persist = NativeMethods.CredPersistEnterprise,
            };
            if (!NativeMethods.CredWriteW(ref cred, 0))
            {
                throw new InvalidOperationException($"CredWrite failed: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
    }

    private readonly record struct CredentialRead(string UserName, string Value);
}
