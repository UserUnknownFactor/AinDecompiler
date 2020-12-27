using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler
{
    public class TemporaryFile : IDisposable
    {
        public string FileName;
        public TemporaryFile(string extension, bool createIt)
        {
        tryAgain:
            string randomFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", "") + "." + extension);
            if (File.Exists(randomFileName) || Directory.Exists(randomFileName))
            {
                goto tryAgain;
            }
            this.FileName = randomFileName;
            if (createIt)
            {
                File.WriteAllBytes(this.FileName, new byte[0]);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (File.Exists(this.FileName))
            {
                File.Delete(this.FileName);
            }
        }

        #endregion
    }

    public class TemporaryDirectory : IDisposable
    {
        public string DirectoryName;
        public TemporaryDirectory(bool createIt)
        {
            tryAgain:
            string randomFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (File.Exists(randomFileName) || Directory.Exists(randomFileName))
            {
                goto tryAgain;
            }
            this.DirectoryName = randomFileName;
            if (createIt)
            {
                Directory.CreateDirectory(this.DirectoryName);
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (Directory.Exists(this.DirectoryName))
            {
                Directory.Delete(this.DirectoryName, true);
            }
        }

        #endregion
    }
}
