// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Microsoft.Maui.Platform.Linux.Services.Passkeys;

/// <summary>
/// FIDO2 security keys through Yubico's libfido2 (<c>libfido2.so.1</c>; Fedora
/// <c>libfido2</c>, Debian and Ubuntu <c>libfido2-1</c>; OpenSSH and systemd use it,
/// so most desktops already have it). USB keys are hidraw devices that systemd's
/// <c>60-fido-id.rules</c> gives the seat's user access to. The library is loaded
/// on first use; without it <see cref="IsAvailable"/> is false.
/// </summary>
internal sealed partial class LibFido2Transport : IFido2Transport
{
    private const string Lib = "libfido2.so.1";
    private const int MaxDevices = 64;

    private static readonly Lazy<bool> s_available = new(Load);

    public static LibFido2Transport Instance { get; } = new();

    public bool IsAvailable => s_available.Value;

    private static bool Load()
    {
        try
        {
            if (!NativeLibrary.TryLoad(Lib, out _))
                return false;
            fido_init(0);
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    public IReadOnlyList<Fido2DeviceInfo> ListDevices()
    {
        if (!IsAvailable)
            return Array.Empty<Fido2DeviceInfo>();

        var list = fido_dev_info_new(MaxDevices);
        if (list == IntPtr.Zero)
            return Array.Empty<Fido2DeviceInfo>();
        try
        {
            if (fido_dev_info_manifest(list, MaxDevices, out var count) != Fido2Error.Success)
                return Array.Empty<Fido2DeviceInfo>();
            var result = new List<Fido2DeviceInfo>((int)count);
            for (nuint i = 0; i < count; i++)
            {
                var info = fido_dev_info_ptr(list, i);
                var path = Marshal.PtrToStringUTF8(fido_dev_info_path(info));
                if (string.IsNullOrEmpty(path))
                    continue;
                result.Add(new Fido2DeviceInfo(path,
                    Marshal.PtrToStringUTF8(fido_dev_info_manufacturer_string(info)),
                    Marshal.PtrToStringUTF8(fido_dev_info_product_string(info))));
            }
            return result;
        }
        finally
        {
            fido_dev_info_free(ref list, MaxDevices);
        }
    }

    public IFido2Device Open(Fido2DeviceInfo info)
    {
        var dev = fido_dev_new();
        if (dev == IntPtr.Zero)
            throw new Fido2Exception(-9, "libfido2 could not allocate a device.");
        var r = fido_dev_open(dev, info.Path);
        if (r != Fido2Error.Success)
        {
            fido_dev_free(ref dev);
            throw Error(r);
        }
        return new Device(info, dev);
    }

    public int SelectByTouch(IReadOnlyList<IFido2Device> devices, CancellationToken cancellationToken)
    {
        var handles = devices.OfType<Device>().ToList();
        foreach (var d in handles)
            fido_dev_get_touch_begin(d.Handle);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                for (var i = 0; i < handles.Count; i++)
                {
                    if (fido_dev_get_touch_status(handles[i].Handle, out var touched, 50) == Fido2Error.Success && touched != 0)
                        return devices.ToList().IndexOf(handles[i]);
                }
            }
            return -1;
        }
        finally
        {
            foreach (var d in handles)
                fido_dev_cancel(d.Handle);
        }
    }

    internal static Fido2Exception Error(int code)
    {
        string message;
        try
        {
            message = Marshal.PtrToStringUTF8(fido_strerr(code)) ?? $"FIDO error {code}";
        }
        catch (Exception)
        {
            message = $"FIDO error {code}";
        }
        return new Fido2Exception(code, message);
    }

    private static void Check(int r)
    {
        if (r != Fido2Error.Success)
            throw Error(r);
    }

    private static unsafe byte[] Read(IntPtr ptr, nuint length)
        => ptr == IntPtr.Zero || length == 0 ? Array.Empty<byte>() : new ReadOnlySpan<byte>((void*)ptr, checked((int)length)).ToArray();

    private sealed class Device : IFido2Device
    {
        private IntPtr _dev;

        public Device(Fido2DeviceInfo info, IntPtr dev)
        {
            Info = info;
            _dev = dev;
        }

        public IntPtr Handle => _dev;

        public Fido2DeviceInfo Info { get; }

        public bool IsFido2 => fido_dev_is_fido2(_dev) != 0;

        public bool HasPin => fido_dev_has_pin(_dev) != 0;

        public bool HasBuiltInUv => fido_dev_has_uv(_dev) != 0;

        public int PinRetries => fido_dev_get_retry_count(_dev, out var retries) == Fido2Error.Success ? retries : -1;

        public void SetTimeout(int milliseconds) => fido_dev_set_timeout(_dev, milliseconds);

        public void Cancel() => fido_dev_cancel(_dev);

        public Fido2Credential MakeCredential(Fido2MakeCredentialRequest request, string? pin)
        {
            var cred = fido_cred_new();
            try
            {
                Check(fido_cred_set_type(cred, request.Algorithm));
                Check(fido_cred_set_clientdata_hash(cred, request.ClientDataHash, (nuint)request.ClientDataHash.Length));
                Check(fido_cred_set_rp(cred, request.RpId, request.RpName));
                Check(fido_cred_set_user(cred, request.UserId, (nuint)request.UserId.Length, request.UserName, request.UserDisplayName, null));
                Check(fido_cred_set_rk(cred, (int)request.ResidentKey));
                Check(fido_cred_set_uv(cred, (int)request.UserVerification));
                foreach (var id in request.ExcludeCredentials)
                    Check(fido_cred_exclude(cred, id, (nuint)id.Length));

                Check(fido_dev_make_cred(_dev, cred, pin));

                return new Fido2Credential(
                    Read(fido_cred_id_ptr(cred), fido_cred_id_len(cred)),
                    Marshal.PtrToStringUTF8(fido_cred_fmt(cred)) ?? "none",
                    Read(fido_cred_authdata_raw_ptr(cred), fido_cred_authdata_raw_len(cred)),
                    Read(fido_cred_attstmt_ptr(cred), fido_cred_attstmt_len(cred)));
            }
            finally
            {
                fido_cred_free(ref cred);
            }
        }

        public IReadOnlyList<Fido2Assertion> GetAssertion(Fido2GetAssertionRequest request, string? pin)
        {
            var assert = fido_assert_new();
            try
            {
                Check(fido_assert_set_rp(assert, request.RpId));
                Check(fido_assert_set_clientdata_hash(assert, request.ClientDataHash, (nuint)request.ClientDataHash.Length));
                foreach (var id in request.AllowCredentials)
                    Check(fido_assert_allow_cred(assert, id, (nuint)id.Length));
                Check(fido_assert_set_uv(assert, (int)request.UserVerification));
                Check(fido_assert_set_up(assert, (int)Fido2Option.True));

                Check(fido_dev_get_assert(_dev, assert, pin));

                var count = fido_assert_count(assert);
                var result = new List<Fido2Assertion>((int)count);
                for (nuint i = 0; i < count; i++)
                {
                    result.Add(new Fido2Assertion(
                        Read(fido_assert_id_ptr(assert, i), fido_assert_id_len(assert, i)),
                        Read(fido_assert_authdata_raw_ptr(assert, i), fido_assert_authdata_raw_len(assert, i)),
                        Read(fido_assert_sig_ptr(assert, i), fido_assert_sig_len(assert, i)),
                        Read(fido_assert_user_id_ptr(assert, i), fido_assert_user_id_len(assert, i)),
                        Marshal.PtrToStringUTF8(fido_assert_user_name(assert, i)),
                        Marshal.PtrToStringUTF8(fido_assert_user_display_name(assert, i))));
                }
                return result;
            }
            finally
            {
                fido_assert_free(ref assert);
            }
        }

        public void Dispose()
        {
            if (_dev == IntPtr.Zero)
                return;
            fido_dev_close(_dev);
            fido_dev_free(ref _dev);
            _dev = IntPtr.Zero;
        }
    }

    [LibraryImport(Lib)] private static partial void fido_init(int flags);
    [LibraryImport(Lib)] private static partial IntPtr fido_strerr(int code);

    [LibraryImport(Lib)] private static partial IntPtr fido_dev_info_new(nuint n);
    [LibraryImport(Lib)] private static partial int fido_dev_info_manifest(IntPtr list, nuint ilen, out nuint olen);
    [LibraryImport(Lib)] private static partial IntPtr fido_dev_info_ptr(IntPtr list, nuint i);
    [LibraryImport(Lib)] private static partial IntPtr fido_dev_info_path(IntPtr info);
    [LibraryImport(Lib)] private static partial IntPtr fido_dev_info_manufacturer_string(IntPtr info);
    [LibraryImport(Lib)] private static partial IntPtr fido_dev_info_product_string(IntPtr info);
    [LibraryImport(Lib)] private static partial void fido_dev_info_free(ref IntPtr list, nuint n);

    [LibraryImport(Lib)] private static partial IntPtr fido_dev_new();
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial int fido_dev_open(IntPtr dev, string path);
    [LibraryImport(Lib)] private static partial int fido_dev_close(IntPtr dev);
    [LibraryImport(Lib)] private static partial void fido_dev_free(ref IntPtr dev);
    [LibraryImport(Lib)] private static partial int fido_dev_cancel(IntPtr dev);
    [LibraryImport(Lib)] private static partial byte fido_dev_is_fido2(IntPtr dev);
    [LibraryImport(Lib)] private static partial byte fido_dev_has_pin(IntPtr dev);
    [LibraryImport(Lib)] private static partial byte fido_dev_has_uv(IntPtr dev);
    [LibraryImport(Lib)] private static partial int fido_dev_get_retry_count(IntPtr dev, out int retries);
    [LibraryImport(Lib)] private static partial int fido_dev_set_timeout(IntPtr dev, int ms);
    [LibraryImport(Lib)] private static partial int fido_dev_get_touch_begin(IntPtr dev);
    [LibraryImport(Lib)] private static partial int fido_dev_get_touch_status(IntPtr dev, out int touched, int ms);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial int fido_dev_make_cred(IntPtr dev, IntPtr cred, string? pin);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial int fido_dev_get_assert(IntPtr dev, IntPtr assert, string? pin);

    [LibraryImport(Lib)] private static partial IntPtr fido_cred_new();
    [LibraryImport(Lib)] private static partial void fido_cred_free(ref IntPtr cred);
    [LibraryImport(Lib)] private static partial int fido_cred_set_type(IntPtr cred, int alg);
    [LibraryImport(Lib)] private static partial int fido_cred_set_clientdata_hash(IntPtr cred, byte[] hash, nuint len);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial int fido_cred_set_rp(IntPtr cred, string id, string? name);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial int fido_cred_set_user(IntPtr cred, byte[] userId, nuint len, string? name, string? displayName, string? icon);
    [LibraryImport(Lib)] private static partial int fido_cred_set_rk(IntPtr cred, int opt);
    [LibraryImport(Lib)] private static partial int fido_cred_set_uv(IntPtr cred, int opt);
    [LibraryImport(Lib)] private static partial int fido_cred_exclude(IntPtr cred, byte[] id, nuint len);
    [LibraryImport(Lib)] private static partial IntPtr fido_cred_id_ptr(IntPtr cred);
    [LibraryImport(Lib)] private static partial nuint fido_cred_id_len(IntPtr cred);
    [LibraryImport(Lib)] private static partial IntPtr fido_cred_fmt(IntPtr cred);
    [LibraryImport(Lib)] private static partial IntPtr fido_cred_authdata_raw_ptr(IntPtr cred);
    [LibraryImport(Lib)] private static partial nuint fido_cred_authdata_raw_len(IntPtr cred);
    [LibraryImport(Lib)] private static partial IntPtr fido_cred_attstmt_ptr(IntPtr cred);
    [LibraryImport(Lib)] private static partial nuint fido_cred_attstmt_len(IntPtr cred);

    [LibraryImport(Lib)] private static partial IntPtr fido_assert_new();
    [LibraryImport(Lib)] private static partial void fido_assert_free(ref IntPtr assert);
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)] private static partial int fido_assert_set_rp(IntPtr assert, string id);
    [LibraryImport(Lib)] private static partial int fido_assert_set_clientdata_hash(IntPtr assert, byte[] hash, nuint len);
    [LibraryImport(Lib)] private static partial int fido_assert_allow_cred(IntPtr assert, byte[] id, nuint len);
    [LibraryImport(Lib)] private static partial int fido_assert_set_uv(IntPtr assert, int opt);
    [LibraryImport(Lib)] private static partial int fido_assert_set_up(IntPtr assert, int opt);
    [LibraryImport(Lib)] private static partial nuint fido_assert_count(IntPtr assert);
    [LibraryImport(Lib)] private static partial IntPtr fido_assert_id_ptr(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial nuint fido_assert_id_len(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial IntPtr fido_assert_authdata_raw_ptr(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial nuint fido_assert_authdata_raw_len(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial IntPtr fido_assert_sig_ptr(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial nuint fido_assert_sig_len(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial IntPtr fido_assert_user_id_ptr(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial nuint fido_assert_user_id_len(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial IntPtr fido_assert_user_name(IntPtr assert, nuint idx);
    [LibraryImport(Lib)] private static partial IntPtr fido_assert_user_display_name(IntPtr assert, nuint idx);
}
