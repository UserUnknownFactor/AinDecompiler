using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler.Compiler
{
    public partial class Compiler
    {
        string mainSourcePrefix;
        string projectName;
        string codeName;
        int gameVersion;

        int _ainVersion;

        int AinVersion
        {
            get
            {
                return _ainVersion;
            }
            set
            {
                _ainVersion = value;

                switch (value)
                {
                    case 0:
                        this.TargetVMVersion = 100;
                        break;
                    case 4:
                        if (this.TargetVMVersion >= 200 && this.TargetVMVersion < 225)
                        {

                        }
                        else
                        {
                            this.TargetVMVersion = 221;
                        }
                        break;
                    case 5:
                        if (this.TargetVMVersion >= 225 && this.TargetVMVersion <= 302)
                        {

                        }
                        else
                        {
                            this.TargetVMVersion = 227;
                        }
                        break;
                    case 6:
                        if (this.TargetVMVersion > 302)
                        {

                        }
                        else
                        {
                            this.TargetVMVersion = 405;
                        }
                        break;
                    case 7:
                        this.TargetVMVersion = 500;
                        break;
                    default:
                        break;
                }
                
                if (value >= 6)
                {
                    GenerateLockPeek = false;
                }
            }
        }

        int TargetVMVersion = 227;

        int KeyCode = 0;
        bool IsAi2File = false;
        bool UsesMsg1 = false;
        bool UseSimplifiedCode = false;

        private void PreprocessInclude(string filename, List<TokenExpression> tokens)
        {
            List<TokenExpression> thisFile = new List<TokenExpression>();
            MyTextReader2.UseEscapedStringsDefault = false;
            IncludeFile(filename, thisFile);
            MyTextReader2.UseEscapedStringsDefault = true;
            int i = 0;
            int insideBrace = 0;
            string sourceDir = Path.GetDirectoryName(filename);

            while (i < thisFile.Count)
            {
                var token = thisFile[i++];
                if (insideBrace > 0)
                {
                    if (token.TokenType == TokenType.OpenBrace)
                    {
                        insideBrace++;
                    }
                    else if (token.TokenType == TokenType.CloseBrace)
                    {
                        insideBrace--;
                    }
                    continue;
                }
                if (token.TokenType == TokenType.OpenBrace)
                {
                    insideBrace++;
                    continue;
                }
                switch (token.Token.Value)
                {
                    case "ProjectName":
                        {
                            var assign = thisFile[i++];
                            if (assign.TokenType == TokenType.Assign)
                            {
                                var filenameToken = thisFile[i++];
                                if (filenameToken.TokenType == TokenType.StringLiteral)
                                {
                                    this.projectName = filenameToken.Token.Value;
                                }
                            }
                        }
                        break;
                    case "CodeName":
                        {
                            var assign = thisFile[i++];
                            if (assign.TokenType == TokenType.Assign)
                            {
                                var filenameToken = thisFile[i++];
                                if (filenameToken.TokenType == TokenType.StringLiteral)
                                {
                                    this.codeName = filenameToken.Token.Value;
                                }
                            }
                        }
                        break;
                    case "SourceDir":
                        {
                            var assign = thisFile[i++];
                            if (assign.TokenType == TokenType.Assign)
                            {
                                var filenameToken = thisFile[i++];
                                if (filenameToken.TokenType == TokenType.StringLiteral)
                                {
                                    sourceDir = Path.Combine(sourceDir, filenameToken.Token.Value);
                                    sourceDir = Path.GetFullPath(sourceDir);
                                    mainSourcePrefix = sourceDir + Path.DirectorySeparatorChar;
                                }
                            }
                        }
                        break;
                    case "GameVersion":
                        {
                            var assign = thisFile[i++];
                            if (assign.TokenType == TokenType.Assign)
                            {
                                var versionToken = thisFile[i++];
                                if (versionToken.TokenType == TokenType.Number)
                                {
                                    this.gameVersion = versionToken.Token.ToInt();
                                }
                            }
                        }
                        break;
                    case "SystemSource":
                    case "Source":
                        {
                            var assign = thisFile[i++];
                            if (assign.TokenType == TokenType.Assign)
                            {
                                var separator = thisFile[i++];
                                if (separator.TokenType == TokenType.OpenBrace)
                                {
                                    while (true)
                                    {
                                        string nextFileName;
                                        var filenameToken = thisFile[i++];
                                        if (filenameToken.TokenType == TokenType.StringLiteral)
                                        {
                                            nextFileName = Path.Combine(sourceDir, filenameToken.Token.Value);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                        separator = thisFile[i++];

                                        nextFileName = Path.Combine(sourceDir, nextFileName);

                                        string ext = Path.GetExtension(nextFileName).ToLowerInvariant();
                                        if (ext == ".jaf")
                                        {
                                            IncludeFile(nextFileName, tokens);
                                            var eofToken = new TokenExpression(TokenType.Eof);
                                            string nextFileName2 = nextFileName;
                                            if (nextFileName2.StartsWith(this.mainSourcePrefix))
                                            {
                                                nextFileName2 = nextFileName2.Substring(this.mainSourcePrefix.Length);
                                            }

                                            var newFilenameToken = new TokenExpression(nextFileName);
                                            newFilenameToken.TokenType = TokenType.StringLiteral;
                                            newFilenameToken.Token.QuoteCharacter = '"';
                                            eofToken.Subexpressions.Add(newFilenameToken);

                                            tokens.Add(eofToken);
                                        }
                                        else if (ext == ".inc")
                                        {
                                            PreprocessInclude(nextFileName, tokens);
                                        }
                                        else if (ext == ".hll")
                                        {
                                            string alias = Path.GetFileNameWithoutExtension(nextFileName);
                                            if (separator.TokenType == TokenType.Comma)
                                            {
                                                var aliasToken = thisFile[i++];
                                                if (aliasToken.TokenType == TokenType.StringLiteral)
                                                {
                                                    alias = aliasToken.Token.Value;
                                                }
                                                separator = thisFile[i++];
                                            }

                                            IncludeHll(nextFileName, alias);
                                        }
                                        if (separator.TokenType == TokenType.CloseBrace)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "#":
                        {
                            var defineToken = thisFile[i];
                            if (defineToken.Token.Value.Equals("define", StringComparison.OrdinalIgnoreCase))
                            {
                                i++;
                                var symbolNameToken = thisFile[i++];
                                var symbolValueToken = thisFile[i++];

                                string symbolName = symbolNameToken.Token.Value;
                                if (symbolName.Equals("_AINVERSION", StringComparison.OrdinalIgnoreCase))
                                {
                                    this.AinVersion = symbolValueToken.Token.ToInt();
                                }
                                else if (symbolName.Equals("_KEYCODE", StringComparison.OrdinalIgnoreCase))
                                {
                                    this.KeyCode = symbolValueToken.Token.ToInt();
                                }
                                else if (symbolName.Equals("_ISAI2FILE", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (symbolValueToken.Token.IsInt())
                                    {
                                        this.IsAi2File = symbolValueToken.Token.ToInt() != 0;
                                    }
                                    else if (symbolValueToken.Token.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                                    {
                                        this.IsAi2File = true;
                                    }
                                    else if (symbolValueToken.Token.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
                                    {
                                        this.IsAi2File = false;
                                    }
                                }
                                else if (symbolName.Equals("_USESMSG1", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (symbolValueToken.Token.IsInt())
                                    {
                                        this.UsesMsg1 = symbolValueToken.Token.ToInt() != 0;
                                    }
                                    else if (symbolValueToken.Token.Value.Equals("true", StringComparison.OrdinalIgnoreCase))
                                    {
                                        this.UsesMsg1 = true;
                                    }
                                    else if (symbolValueToken.Token.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
                                    {
                                        this.UsesMsg1 = false;
                                    }
                                }
                                else if (symbolName.Equals("_TARGETVM", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (symbolValueToken.Token.IsInt())
                                    {
                                        this.TargetVMVersion = symbolValueToken.Token.ToInt();
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }

        private bool IncludeHll(string hllFile, string alias)
        {
            if (!File.Exists(hllFile))
            {
                return false;
            }
            List<TokenExpression> tokens2 = new List<TokenExpression>();
            IncludeFile(hllFile, tokens2);
            var firstPass = new FirstPass();
            var tokens3 = firstPass.CompileFirstPass(tokens2);
            if (firstPass.Errors.Count > 0)
            {
                return false;
            }
            CreateHllSymbols(tokens3, Path.GetFileNameWithoutExtension(hllFile), alias);
            return true;
        }

        private void LoadHllFiles(string libraryPath)
        {
            string[] hllFiles = Directory.GetFiles(libraryPath, "*.hll", SearchOption.AllDirectories);
            List<TokenExpression> tokens2 = new List<TokenExpression>();
            foreach (string hllFile in hllFiles)
            {
                IncludeFile(hllFile, tokens2);
                var firstPass = new FirstPass();
                var tokens3 = firstPass.CompileFirstPass(tokens2);
                string libraryName = Path.GetFileNameWithoutExtension(hllFile);
                CreateHllSymbols(tokens3, libraryName, libraryName);
                tokens2.Clear();
            }
        }

        void IncludeFile(string fileName, List<TokenExpression> tokens)
        {
            var encoding = EncodingDetector.DetectEncoding(fileName);
            var trBase = new StreamReader(fileName, encoding);
            var tr = new MyTextReader2(trBase, fileName);
            IncludeFile(tr, tokens, fileName);
        }

        void IncludeString(string expression, List<TokenExpression> tokens)
        {
            var sr = new StringReader(expression);
            var tr = new MyTextReader2(sr, "");
            IncludeFile(tr, tokens, "");
        }

        HashSet<string> seenFileNames = new HashSet<string>();

        private void IncludeFile(MyTextReader2 tr, List<TokenExpression> tokens, string fileName)
        {
            while (true)
            {
                tr.EatWhiteSpace();
                int row = tr.Row;
                int column = tr.Column;
                string trFileName = tr.FileName;
                var token = tr.ReadToken();

                if (token == null)
                {
                    break;
                }
                var tokenExpression = new TokenExpression(token);
                tokenExpression.Row = row;
                tokenExpression.Column = column;
                tokenExpression.FileName = trFileName;
                if (tokenExpression.TokenType == TokenType.Pound)
                {
                    var includeToken = tr.PeekToken();
                    if (includeToken.Value.Equals("include", StringComparison.OrdinalIgnoreCase))
                    {
                        includeToken = tr.ReadToken();
                        var fileNameToken = tr.PeekToken();
                        if (fileNameToken.IsQuoted)
                        {
                            fileNameToken = tr.ReadToken();
                            //If it's a .HLL file, load it as an HLL file instead
                            string includeFileName = tr.CanonicalizeFileName(fileNameToken.Value);
                            string includeExt = Path.GetExtension(includeFileName).ToLowerInvariant();
                            if (includeExt == ".hll")
                            {
                                string alias = Path.GetFileNameWithoutExtension(includeFileName);
                                IncludeHll(includeFileName, alias);
                            }
                            else
                            {
                                try
                                {
                                    tr.IncludeTextReader(includeFileName);
                                }
                                catch (IOException)
                                {
                                    throw;
                                }
                            }
                            continue;
                        }
                        else
                        {
                            tr.PutbackToken(includeToken);
                        }
                    }
                }

                if (tokenExpression.IsMacro())
                {
                    switch (tokenExpression.TokenType)
                    {
                        case TokenType.FileMacro:
                            tokenExpression.Token = new Token(tr.FileName);
                            tokenExpression.Token.QuoteCharacter = '"';
                            tokenExpression.TokenType = TokenType.StringLiteral;
                            break;
                        case TokenType.LineMacro:
                            tokenExpression.Token = new Token((row + 1).ToString());
                            tokenExpression.Token.QuoteCharacter = '"';
                            tokenExpression.TokenType = TokenType.StringLiteral;
                            break;
                        case TokenType.FuncMacro:
                            break;
                        case TokenType.DateMacro:
                            StringBuilder dateString = new StringBuilder();
                            dateString.Append(DateTime.Today.ToString("yyyy/MM/dd"));
                            if (dateString[5] == '0') dateString[5] = ' ';
                            if (dateString[8] == '0') dateString[8] = ' ';
                            tokenExpression.Token = new Token(dateString.ToString());
                            tokenExpression.Token.QuoteCharacter = '"';
                            tokenExpression.TokenType = TokenType.StringLiteral;
                            break;
                        case TokenType.TimeMacro:
                            tokenExpression.Token = new Token(DateTime.Now.ToString("HH:mm:ss"));
                            tokenExpression.Token.QuoteCharacter = '"';
                            tokenExpression.TokenType = TokenType.StringLiteral;
                            break;
                    }
                }
                tokens.Add(tokenExpression);
            }
        }


    }
}
