using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BluffAuction.Platform
{
    /// <summary>
    /// 윈도우 '열기' 대화상자 (comdlg32). 그림 파일을 고를 때 쓴다. 대화상자가 떠 있는 동안 게임은 잠깐 멈춘다.
    /// 윈도우가 아니거나 실패하면 null.
    /// </summary>
    public static class FileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class OpenFileName
        {
            public int lStructSize = Marshal.SizeOf(typeof(OpenFileName));
            public IntPtr hwndOwner = IntPtr.Zero;
            public IntPtr hInstance = IntPtr.Zero;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData = IntPtr.Zero;
            public IntPtr lpfnHook = IntPtr.Zero;
            public string lpTemplateName;
            public IntPtr pvReserved = IntPtr.Zero;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool GetOpenFileNameW([In, Out] OpenFileName ofn);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        const int OFN_PATHMUSTEXIST = 0x800, OFN_FILEMUSTEXIST = 0x1000, OFN_NOCHANGEDIR = 0x8, OFN_EXPLORER = 0x80000;

        public static string OpenImage(string title, string filterName)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) return null;
            try
            {
                var ofn = new OpenFileName
                {
                    hwndOwner = GetActiveWindow(),
                    lpstrFilter = filterName + " (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0",
                    lpstrFile = new string('\0', 1024),
                    nMaxFile = 1024,
                    lpstrFileTitle = new string('\0', 256),
                    nMaxFileTitle = 256,
                    lpstrInitialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    lpstrTitle = title,
                    Flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST | OFN_NOCHANGEDIR
                };
                if (!GetOpenFileNameW(ofn)) return null;
                string path = ofn.lpstrFile;
                int z = path.IndexOf('\0');
                if (z >= 0) path = path.Substring(0, z);
                return string.IsNullOrEmpty(path) ? null : path;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FileDialog] " + e.Message);
                return null;
            }
        }
    }
}
