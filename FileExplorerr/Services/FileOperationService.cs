using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FileExplorerr
{
    // ════════════════════════════════════════════════════════════════════════
    //  FILE OPERATION SERVICE
    //  Filesystem operations performed from the main explorer window:
    //  create folder, rename, delete (to Recycle Bin), and move via DnD.
    //
    //  Phase 5A: extracted from Form1.cs.
    //  Original methods removed from Form1:
    //    CreateFolder()                 -> FileOperationService.CreateFolder(...)
    //    RenameSelected()               -> FileOperationService.RenameSelected(...)
    //    DeleteSelected()               -> FileOperationService.DeleteSelected(...)
    //    MoveItems(string[], string)    -> FileOperationService.MoveItems(...)
    //    SendToRecycleBin(string)       -> private here (was private on Form1)
    //
    //  The SHFILEOPSTRUCT P/Invoke is reproduced verbatim from Form1.
    //  The caller (Form1) passes its Handle so this class has no dependency
    //  on Form1's own window handle.
    //
    //  After each mutating operation the caller is expected to call its own
    //  directory-refresh method; this is injected as an Action callback.
    // ════════════════════════════════════════════════════════════════════════
    internal static class FileOperationService
    {
        // ── P/Invoke — SHFileOperation (Recycle Bin) ──────────────────────
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.U4)] public int wFunc;
            public string? pFrom;
            public string? pTo;
            public short fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string? lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

        private const int FO_DELETE = 3;
        private const int FOF_ALLOWUNDO = 0x40;
        private const int FOF_NOCONFIRMATION = 0x10;

        // ════════════════════════════════════════════════════════════════════
        //  CREATE FOLDER
        //  Originally: private void CreateFolder() on Form1.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Prompts for a folder name via <see cref="InputDialog"/> and creates
        /// the folder inside <paramref name="currentPath"/>.
        /// Calls <paramref name="onRefresh"/> on success so Form1 reloads the view.
        /// <para>
        /// Call-site: replace <c>CreateFolder()</c>
        ///             with    <c>FileOperationService.CreateFolder(currentPath, this, LoadDirectory)</c>.
        /// </para>
        /// </summary>
        public static void CreateFolder(
            string currentPath,
            IWin32Window owner,
            Action onRefresh)
        {
            string? name = InputDialog.Show(owner, "Nueva carpeta", "Nombre:");
            if (string.IsNullOrWhiteSpace(name)) return;

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("Nombre no v\u00E1lido.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string newDir = Path.Combine(currentPath, name);

            if (Directory.Exists(newDir))
            {
                MessageBox.Show("Ya existe.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Directory.CreateDirectory(newDir);
                onRefresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  RENAME SELECTED
        //  Originally: private void RenameSelected() on Form1.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Shows a rename prompt for <paramref name="oldPath"/> and renames
        /// the file or directory if the user provides a new name.
        /// <para>
        /// Call-site: replace <c>RenameSelected()</c>
        ///             with    <c>FileOperationService.RenameSelected(selectedPath, this, RefreshView)</c>.
        /// </para>
        /// </summary>
        public static void RenameSelected(
            string oldPath,
            IWin32Window owner,
            Action onRefresh)
        {
            string oldName = Path.GetFileName(oldPath);
            string? newName = InputDialog.Show(owner, "Renombrar", "Nuevo nombre:", oldName);

            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

            string newPath = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);

            try
            {
                if (File.Exists(oldPath))
                    File.Move(oldPath, newPath);
                else if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);

                onRefresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  DELETE SELECTED
        //  Originally: private void DeleteSelected() on Form1.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Asks for confirmation then sends each path in
        /// <paramref name="paths"/> to the Recycle Bin.
        /// <para>
        /// Call-site: replace <c>DeleteSelected()</c> with
        /// <c>FileOperationService.DeleteSelected(selectedPaths, Handle, this, RefreshView)</c>.
        /// </para>
        /// </summary>
        public static void DeleteSelected(
            string[] paths,
            IntPtr hwnd,
            IWin32Window owner,
            Action onRefresh)
        {
            if (paths.Length == 0) return;

            string msg = paths.Length == 1
                ? $"\u00BFEliminar \"{Path.GetFileName(paths[0])}\"?"
                : $"\u00BFEliminar {paths.Length} elementos?";

            if (MessageBox.Show(msg, "Confirmar",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            foreach (string p in paths)
                SendToRecycleBin(p, hwnd);

            onRefresh();
        }

        // ════════════════════════════════════════════════════════════════════
        //  MOVE ITEMS
        //  Originally: private void MoveItems(string[] sources, string targetDir)
        //  on Form1.  Logic is identical to the original.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Moves each item in <paramref name="sources"/> into
        /// <paramref name="targetDir"/>, handling name conflicts interactively.
        /// <para>
        /// Call-site: replace <c>MoveItems(sources, targetDir)</c>
        ///             with    <c>FileOperationService.MoveItems(sources, targetDir, this, RefreshView)</c>.
        /// </para>
        /// </summary>
        public static void MoveItems(
            string[] sources,
            string targetDir,
            IWin32Window owner,
            Action onRefresh)
        {
            foreach (string src in sources)
            {
                try
                {
                    string name = Path.GetFileName(
                        src.TrimEnd(Path.DirectorySeparatorChar));
                    string dest = Path.Combine(targetDir, name);

                    if (dest.Equals(src, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (File.Exists(src))
                    {
                        if (File.Exists(dest))
                        {
                            var r = MessageBox.Show(
                                $"Ya existe \"{name}\". \u00BFSobreescribir?",
                                "Conflicto",
                                MessageBoxButtons.YesNoCancel,
                                MessageBoxIcon.Question);

                            if (r == DialogResult.Cancel) return;
                            if (r == DialogResult.No) continue;

                            File.Delete(dest);
                        }
                        File.Move(src, dest);
                    }
                    else if (Directory.Exists(src))
                    {
                        if (targetDir.StartsWith(
                                src + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            MessageBox.Show(
                                "No se puede mover dentro de s\u00ED misma.",
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                            continue;
                        }
                        Directory.Move(src, dest);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            onRefresh();
        }

        // ════════════════════════════════════════════════════════════════════
        //  RECYCLE BIN — internal; exposed for RecycleDragDrop on Form1
        //  Originally: private bool SendToRecycleBin(string path) on Form1.
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends <paramref name="path"/> to the Recycle Bin using
        /// SHFileOperation with FOF_ALLOWUNDO, exactly as Form1 did.
        /// </summary>
        internal static bool SendToRecycleBin(string path, IntPtr hwnd)
        {
            try
            {
                var op = new SHFILEOPSTRUCT
                {
                    hwnd = hwnd,
                    wFunc = FO_DELETE,
                    pFrom = path + '\0' + '\0',
                    fFlags = (short)(FOF_ALLOWUNDO | FOF_NOCONFIRMATION)
                };
                return SHFileOperation(ref op) == 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FileOperationService.SendToRecycleBin] {ex.Message}");
                return false;
            }
        }
    }
}