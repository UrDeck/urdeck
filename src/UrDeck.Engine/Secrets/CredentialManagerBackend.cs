// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace UrDeck.Engine.Secrets;

/// <summary>
/// Secrets as Windows Credential Manager generic credentials with the target <c>&lt;prefix&gt;/&lt;provider&gt;/&lt;setting&gt;</c>
/// (prefix <c>UrDeck</c> in the product), kept for this user on this PC (<c>CRED_PERSIST_LOCAL_MACHINE</c>, not roamed). The
/// credential's user name holds the context the secret was saved for. Visible and deletable in Control Panel.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class CredentialManagerBackend : ISecretBackend
{
    /// <summary>The prefix of every credential UrDeck stores; the uninstall hook deletes all that start with it.</summary>
    public const string ProductPrefix = "UrDeck";

    private const int TypeGeneric = 1;
    private const int PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    private readonly string _prefix;

    /// <param name="prefix">The first segment of every target. Tests give their own so they never touch a real credential.</param>
    public CredentialManagerBackend(string prefix = ProductPrefix)
    {
        _prefix = prefix;
    }

    private string Target(string providerId, string setting) => $"{_prefix}/{providerId}/{setting}";

    public StoredSecret? Read(string providerId, string setting)
    {
        if (!Native.CredRead(Target(providerId, setting), TypeGeneric, 0, out nint pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return null;
            throw new Win32Exception(error);
        }

        try
        {
            return Decode(Marshal.PtrToStructure<Native.Credential>(pointer));
        }
        finally
        {
            Native.CredFree(pointer);
        }
    }

    public void Write(string providerId, string setting, string value, string? context)
    {
        byte[] blob = Encoding.UTF8.GetBytes(value);
        if (blob.Length > SecretStore.MaxSecretBytes)
            throw new SecretStoreException($"The secret is larger than Credential Manager accepts ({SecretStore.MaxSecretBytes:N0} bytes).");

        nint blobPointer = Marshal.AllocHGlobal(Math.Max(blob.Length, 1));
        try
        {
            Marshal.Copy(blob, 0, blobPointer, blob.Length);
            var credential = new Native.Credential
            {
                Type = TypeGeneric,
                TargetName = Target(providerId, setting),
                UserName = context,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPointer,
                Persist = PersistLocalMachine,
            };
            if (!Native.CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.FreeHGlobal(blobPointer);
        }
    }

    public bool Delete(string providerId, string setting)
    {
        if (Native.CredDelete(Target(providerId, setting), TypeGeneric, 0))
            return true;
        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return false;
        throw new Win32Exception(error);
    }

    public int DeleteAll()
    {
        int deleted = 0;
        foreach (string target in Targets())
        {
            if (Native.CredDelete(target, TypeGeneric, 0))
                deleted++;
        }

        return deleted;
    }

    public IReadOnlyList<(string ProviderId, string Setting)> List()
    {
        var list = new List<(string, string)>();
        foreach (string target in Targets())
        {
            string[] parts = target.Substring(_prefix.Length + 1).Split('/');
            if (parts.Length == 2)
                list.Add((parts[0], parts[1]));
        }

        return list;
    }

    /// <summary>The targets of every generic credential that starts with the prefix.</summary>
    private List<string> Targets()
    {
        var targets = new List<string>();
        if (!Native.CredEnumerate(_prefix + "/*", 0, out int count, out nint array))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return targets;
            throw new Win32Exception(error);
        }

        try
        {
            for (int i = 0; i < count; i++)
            {
                nint item = Marshal.ReadIntPtr(array, i * nint.Size);
                var credential = Marshal.PtrToStructure<Native.Credential>(item);
                if (credential.Type == TypeGeneric && credential.TargetName is { } target
                    && target.StartsWith(_prefix + "/", StringComparison.Ordinal))
                    targets.Add(target);
            }
        }
        finally
        {
            Native.CredFree(array);
        }

        return targets;
    }

    private static StoredSecret Decode(Native.Credential credential)
    {
        byte[] blob = new byte[credential.CredentialBlobSize];
        if (blob.Length > 0)
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
        return new StoredSecret(Encoding.UTF8.GetString(blob), string.IsNullOrEmpty(credential.UserName) ? null : credential.UserName);
    }

    private static class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct Credential
        {
            public uint Flags;
            public uint Type;
            public string? TargetName;
            public string? Comment;
            public long LastWritten;
            public uint CredentialBlobSize;
            public nint CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public nint Attributes;
            public string? TargetAlias;
            public string? UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredRead(string target, uint type, uint flags, out nint credential);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredWrite(ref Credential credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredEnumerateW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CredEnumerate(string filter, uint flags, out int count, out nint credentials);

        [DllImport("advapi32.dll")]
        public static extern void CredFree(nint buffer);
    }
}
