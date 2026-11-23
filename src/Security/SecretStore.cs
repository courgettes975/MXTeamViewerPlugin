namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

    // Secrets (only the optional TeamViewer Web API token today) are stored with the Windows Data
    // Protection API, scope CurrentUser, in %LOCALAPPDATA%\MXTeamViewerPlugin\secrets\<name>.bin.
    // Only the same Windows user on the same machine can decrypt them. Never in config.json, never
    // logged. scripts\Set-ApiToken.ps1 writes the same format (same entropy) from PowerShell.
    //
    // TeamViewer PASSWORDS are deliberately NOT supported: connections are opened without a password
    // and TeamViewer itself asks for it (or uses Easy Access / account assignment).
    internal static class SecretStore
    {
        // Not a secret: an application-specific salt so other DPAPI blobs of the user don't match.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MXTeamViewerPlugin.v1");

        public static string Directory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MXTeamViewerPlugin", "secrets");

        public static bool Has(string name) => File.Exists(PathOf(name));

        public static string Get(string name)
        {
            try
            {
                var path = PathOf(name);
                return File.Exists(path) ? Encoding.UTF8.GetString(Unprotect(File.ReadAllBytes(path))) : null;
            }
            catch (Exception ex)
            {
                TvLog.Warning($"Secret '{name}' cannot be decrypted (other user/machine?)", ex);
                return null;
            }
        }

        public static void Set(string name, string value)
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllBytes(PathOf(name), Protect(Encoding.UTF8.GetBytes(value)));
        }

        public static void Delete(string name)
        {
            var path = PathOf(name);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string PathOf(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return Path.Combine(Directory, name + ".bin");
        }

        // ---- DPAPI (crypt32) ----------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct DATA_BLOB
        {
            public int cbData;
            public IntPtr pbData;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DATA_BLOB dataIn, string description, ref DATA_BLOB entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DATA_BLOB dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DATA_BLOB dataIn, IntPtr description, ref DATA_BLOB entropy,
            IntPtr reserved, IntPtr prompt, int flags, ref DATA_BLOB dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr mem);

        private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

        private static byte[] Protect(byte[] data) => Transform(data, true);

        private static byte[] Unprotect(byte[] data) => Transform(data, false);

        private static byte[] Transform(byte[] data, bool protect)
        {
            var input = ToBlob(data);
            var entropy = ToBlob(Entropy);
            var output = new DATA_BLOB();
            try
            {
                var ok = protect
                    ? CryptProtectData(ref input, "MXTeamViewerPlugin", ref entropy, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref output);
                if (!ok)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                var result = new byte[output.cbData];
                Marshal.Copy(output.pbData, result, 0, output.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(input.pbData);
                Marshal.FreeHGlobal(entropy.pbData);
                if (output.pbData != IntPtr.Zero)
                {
                    LocalFree(output.pbData);
                }
            }
        }

        private static DATA_BLOB ToBlob(byte[] data)
        {
            var blob = new DATA_BLOB { cbData = data.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, data.Length)) };
            Marshal.Copy(data, 0, blob.pbData, data.Length);
            return blob;
        }
    }
}
