using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AinDecompiler
{
    public enum DialogTopic
    {
        LoadAinFile,
        DecompileCode,
        CompileCode,
        DisassembleCode,
        AssembleCode,
        AssembleCodeSaveAin,
        ExportText,
        ImportText,
        ImportTextSaveAin,
        QuickCompileCode,
        QuickCompileCodeSaveAin,
        SaveDocument,
        OpenDocument,
    }

    public static partial class DialogExtensions
    {
        public static DialogResult ShowDialogWithTopic(this FileDialog fileDialog, DialogTopic topic)
        {
            string loadedAinFileName = "";
            if (Expression.defaultAinFile != null)
            {
                loadedAinFileName = Expression.defaultAinFile.OriginalFilename ?? "";
            }
            string ainFileName = Path.GetFileNameWithoutExtension(loadedAinFileName).ToLowerInvariant();
            string path = GetPath(ainFileName, topic);
            if (String.IsNullOrEmpty(path) && !String.IsNullOrEmpty(loadedAinFileName))
            {
                path = Path.GetDirectoryName(loadedAinFileName);
            }
            if (!String.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                fileDialog.InitialDirectory = path;
            }
            DialogResult dialogResult = DialogResult.None;
            if (String.IsNullOrEmpty(fileDialog.FileName))
            {
                switch (topic)
                {
                    case DialogTopic.AssembleCode:
                    case DialogTopic.CompileCode:
                    case DialogTopic.ImportText:
                    case DialogTopic.LoadAinFile:
                        if (!string.IsNullOrEmpty(fileDialog.DefaultExt))
                        {
                            if (Directory.Exists(path))
                            {
                                string[] matchingFiles = Directory.GetFiles(path, "*." + fileDialog.DefaultExt, SearchOption.TopDirectoryOnly);
                                if (matchingFiles.Length == 1)
                                {
                                    fileDialog.FileName = Path.GetFileName(matchingFiles[0]);
                                }
                            }
                        }
                        break;
                }
            }
            
            
            if (fileDialog is SaveFileDialog)
            {
                dialogResult = ((SaveFileDialog)fileDialog).ShowDialogEx();
            }
            else
            {
                dialogResult = fileDialog.ShowDialog();
            }
            if (dialogResult == DialogResult.OK)
            {
                path = Path.GetDirectoryName(fileDialog.FileName);
                if (topic != DialogTopic.LoadAinFile)
                {
                    SavePath(ainFileName, topic, path);
                }
                SavePath("", topic, path);
            }
            return dialogResult;
        }

        private static string GetPath(string ainFileName, DialogTopic topic)
        {
            string path = "";
            if (!TryGetPath(ainFileName, topic, ref path))
            {
                switch (topic)
                {
                    case DialogTopic.LoadAinFile:
                    case DialogTopic.AssembleCodeSaveAin:
                    case DialogTopic.QuickCompileCodeSaveAin:
                    case DialogTopic.ImportTextSaveAin:
                        TryGetPath(ainFileName, DialogTopic.LoadAinFile, ref path);
                        TryGetPath(ainFileName, DialogTopic.AssembleCodeSaveAin, ref path);
                        TryGetPath(ainFileName, DialogTopic.ImportTextSaveAin, ref path);
                        TryGetPath(ainFileName, DialogTopic.QuickCompileCodeSaveAin, ref path);
                        break;
                    case DialogTopic.DecompileCode:
                    case DialogTopic.CompileCode:
                        TryGetPath(ainFileName, DialogTopic.CompileCode, ref path);
                        TryGetPath(ainFileName, DialogTopic.DecompileCode, ref path);
                        break;
                    case DialogTopic.DisassembleCode:
                    case DialogTopic.AssembleCode:
                        TryGetPath(ainFileName, DialogTopic.AssembleCode, ref path);
                        TryGetPath(ainFileName, DialogTopic.DisassembleCode, ref path);
                        break;
                    case DialogTopic.ExportText:
                    case DialogTopic.ImportText:
                        TryGetPath(ainFileName, DialogTopic.ExportText, ref path);
                        TryGetPath(ainFileName, DialogTopic.ImportText, ref path);
                        break;
                    case DialogTopic.QuickCompileCode:
                        break;
                    case DialogTopic.SaveDocument:
                    case DialogTopic.OpenDocument:
                        TryGetPath(ainFileName, DialogTopic.OpenDocument, ref path);
                        TryGetPath(ainFileName, DialogTopic.SaveDocument, ref path);
                        break;
                }
            }
            //if (!String.IsNullOrEmpty(ainFileName) && String.IsNullOrEmpty(path))
            //{
            //    return GetPath("", topic);
            //}
            return path;
        }

        private static bool TryGetPath(string ainFileName, DialogTopic topic, ref string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                if (String.IsNullOrEmpty(ainFileName))
                {
                    ainFileName = "Default";
                }
                string regPath = "Directories\\" + ainFileName;
                string regKey = topic.ToString();
                path = RegistryUtility.GetSetting(regKey, regPath, "") ?? "";
                if (String.IsNullOrEmpty(path))
                {
                    return false;
                }
                return true;
            }
            return false;
        }
        private static void SavePath(string ainFileName, DialogTopic topic, string path)
        {
            if (String.IsNullOrEmpty(ainFileName))
            {
                ainFileName = "Default";
            }
            string regPath = "Directories\\" + ainFileName;
            string regKey = topic.ToString();
            RegistryUtility.SaveSetting(regKey, regPath, path);
        }



        public static DialogResult ShowDialogEx(this SaveFileDialog saveFileDialog)
        {
        tryAgain:
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                if (File.Exists(saveFileDialog.FileName))
                {
                    var fileInfo = new FileInfo(saveFileDialog.FileName);
                    if (fileInfo.IsReadOnly)
                    {
                        if (MessageBox.Show("The selected file is read-only.  Overwrite the file anyway?", "Overwrite Read-Only File", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                        {
                            try
                            {
                                fileInfo.IsReadOnly = false;
                                var fs = new FileStream(saveFileDialog.FileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                                fs.Close();
                                return DialogResult.OK;
                            }
                            catch
                            {

                            }
                            goto tryAgain;
                        }
                    }
                    else
                    {
                        return DialogResult.OK;
                    }
                }
                else
                {
                    return DialogResult.OK;
                }
            }
            return DialogResult.Cancel;
        }
    }
}