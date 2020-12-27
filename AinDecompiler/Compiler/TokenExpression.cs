using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.CodeDom.Compiler;
using System.Collections.ObjectModel;
using AinDecompiler;

namespace AinDecompiler.Compiler
{
    public class TokenExpression
    {
        internal static readonly KeywordTable keywordTable = new KeywordTable();
        internal static readonly Dictionary<TokenType, string> keywordTableInverse = keywordTable.Inverse();

        public TokenExpression Parent;
        public Token Token;
        public int row = -1;
        public int column = -1;
        public int Row
        {
            get
            {
                if (row == -1)
                {
                    foreach (var token in GetAllSubexpressionsRecursive())
                    {
                        if (token.row != -1)
                        {
                            return token.row;
                        }
                    }
                }
                return row;
            }
            set
            {
                row = value;
            }
        }
        public int Column
        {
            get
            {
                if (column == -1)
                {
                    foreach (var token in GetAllSubexpressionsRecursive())
                    {
                        if (token.column != -1)
                        {
                            return token.column;
                        }
                    }
                }
                return column;
            }
            set
            {
                column = value;
            }
        }
        public string FileName
        {
            get
            {
                if (fileName == null)
                {
                    foreach (var token in GetAllSubexpressionsRecursive())
                    {
                        if (token.fileName != null)
                        {
                            return token.fileName;
                        }
                    }
                    var parent = this.Parent;
                    while (parent != null)
                    {
                        if (parent.fileName != null)
                        {
                            return parent.fileName;
                        }
                        parent = parent.Parent;
                    }
                }
                return fileName;
            }
            set
            {
                fileName = value;
            }
        }
        public string fileName;

        public TokenType TokenType;
        public TokenExpression(IEnumerable<TokenExpression> expressions, TokenType tokenType)
            : this(new Token("..."))
        {
            if (this.Subexpressions == null) { this.Subexpressions = new TokenExpressionCollection(this); }
            this.TokenType = tokenType;
            if (expressions != null)
            {
                Subexpressions.AddRange(expressions);
            }

            if (tokenType == TokenType.First)
            {
                var first = Subexpressions.FirstOrDefault();
                if (first != null)
                {
                    this.TokenType = first.TokenType;
                }
            }
        }

        public TokenExpression(IEnumerable<TokenExpression> expressions)
            : this(expressions, TokenType.Block)
        {
            if (this.Subexpressions == null) { this.Subexpressions = new TokenExpressionCollection(this); }

        }

        public TokenExpression(TokenType tokenType)
            : this(tokenType.ToString())
        {
            if (this.Subexpressions == null) { this.Subexpressions = new TokenExpressionCollection(this); }
            this.TokenType = tokenType;
        }

        public TokenExpression(string text)
            : this(new Token(text))
        {
            if (this.Subexpressions == null) { this.Subexpressions = new TokenExpressionCollection(this); }
        }

        public TokenExpression()
            : this("...")
        {
            if (this.Subexpressions == null) { this.Subexpressions = new TokenExpressionCollection(this); }
            this.TokenType = TokenType.Block;
        }

        public TokenExpression(Token token)
        {
            if (this.Subexpressions == null) { this.Subexpressions = new TokenExpressionCollection(this); }
            this.Token = token;
            if (token.QuoteCharacter != 0)
            {
                if (token.QuoteCharacter == '"')
                {
                    this.TokenType = TokenType.StringLiteral;
                    return;
                }
                else if (token.QuoteCharacter == '\'')
                {
                    this.TokenType = TokenType.Message;
                    return;
                }
            }
            else if (token.IsNumber())
            {
                this.TokenType = TokenType.Number;
                return;
            }
            this.TokenType = keywordTable.GetOrDefault(token.Value, TokenType.Identifier);
            if (this.TokenType != TokenType.Identifier)
            {
                this.Token.Value = String.Intern(this.Token.Value);
            }
        }
        public TokenExpressionCollection Subexpressions;

        public bool IsMacro()
        {
            if (TokenType >= TokenType.FileMacro && TokenType <= TokenType.TimeMacro)
            {
                return true;
            }
            return false;
        }

        public bool IsDataType()
        {
            switch (TokenType)
            {
                case TokenType.Void:
                case TokenType.Int:
                case TokenType.Bool:
                case TokenType.String:
                case TokenType.Float:
                case TokenType.Lint:
                case TokenType.IMainSystem:
                    return true;
            }
            return false;
        }

        public bool IsBuiltInMethod()
        {
            return false;
            //return TokenType >= TokenType.Numof && TokenType <= TokenType.ConvertToInt;
        }

        private string ToStringLeaf()
        {
            if (this.TokenType == TokenType.Identifier ||
                this.TokenType == TokenType.Number ||
                this.TokenType == TokenType.StringLiteral ||
                this.TokenType == TokenType.Message)
            {
                if (this.Token.QuoteCharacter != 0)
                {
                    return this.Token.QuoteCharacter + this.Token.Value + this.Token.QuoteCharacter;
                }
                return this.Token.Value;
            }
            else
            {
                return this.TokenType.ToString();
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(this.ToStringLeaf());

            if (this.Subexpressions.Count > 0)
            {
                int limit = Math.Min(this.Subexpressions.Count, 2);
                for (int i = 0; i < limit; i++)
                {
                    sb.Append(" ");
                    var subexpression = this.Subexpressions[i];
                    if (subexpression == null)
                    {
                        sb.Append("null");
                    }
                    else
                    {
                        sb.Append(subexpression.ToStringLeaf());
                        if (subexpression.Subexpressions.Count > 0)
                        {
                            sb.Append("...");
                        }
                    }
                }
            }
            return sb.ToString();
        }

        public string ToIndentedString()
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);
            IndentedTextWriter tw = new IndentedTextWriter(sw, " ");
            this.WriteIndentedString(tw);

            tw.Flush();
            return sb.ToString();
        }

        private void WriteIndentedString(IndentedTextWriter tw)
        {
            int depth = tw.Indent;
            if (this.TokenType == TokenType.Identifier)
            {
                tw.WriteLine(this.Token.Value);
            }
            else
            {
                tw.WriteLine(this.TokenType.ToString());
            }

            if (this.Subexpressions.Count > 0)
            {
                tw.Indent++;
                if (depth >= 10)
                {
                    tw.WriteLine("...");
                }
                else
                {
                    for (int i = 0; i < this.Subexpressions.Count; i++)
                    {
                        var subexpression = this.Subexpressions[i];
                        if (subexpression == null)
                        {
                            tw.WriteLine("null");
                        }
                        else
                        {
                            subexpression.WriteIndentedString(tw);
                        }
                    }
                }
                tw.Indent--;
            }
        }

        static string ToStringReal(TokenExpression ex)
        {
            if (ex == null) return "";
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);
            MyIndentedTextWriter tw = new MyIndentedTextWriter(sw);
            ToStringReal(ex, tw);
            return sb.ToString();
        }

        static void ToStringReal(IEnumerable<TokenExpression> subexpressions, MyIndentedTextWriter tw)
        {
            foreach (var subexpression in subexpressions)
            {
                ToStringReal(subexpression, tw);
            }
        }

        static void ToStringReal(TokenExpression ex, MyIndentedTextWriter tw)
        {
            if (ex == null) return;
            switch (ex.TokenType)
            {
                default:
                    if (!String.IsNullOrEmpty(ex.Token.Value) && ex.Token.Value != "...")
                    {
                        tw.Write(ex.Token.Value);
                    }
                    if (ex.Subexpressions.Count > 0)
                    {
                        tw.Write(" ");
                        ToStringReal(ex.Subexpressions, tw);
                    }
                    break;
                case TokenType.Array:
                case TokenType.AddressOf:
                case TokenType.At:
                case TokenType.Complement:
                case TokenType.Increment:
                case TokenType.Decrement:
                    if (!String.IsNullOrEmpty(ex.Token.Value) && ex.Token.Value != "...")
                    {
                        tw.Write(ex.Token.Value);
                    }
                    if (ex.Subexpressions.Count > 0)
                    {
                        ToStringReal(ex.Subexpressions, tw);
                    }
                    break;
                case TokenType.Block:
                    if (!tw.TabsPending)
                    {
                        tw.WriteLine();
                    }
                    tw.WriteLine("{");
                    tw.Indent++;
                    ToStringReal(ex.Subexpressions, tw);
                    tw.Indent--;
                    if (!tw.TabsPending)
                    {
                        tw.WriteLine();
                    }
                    tw.WriteLine("}");
                    break;
                case TokenType.Statement:
                    ToStringReal(ex.Subexpressions, tw);
                    tw.WriteLine(";");
                    break;
                case TokenType.StringLiteral:
                    tw.Write(AssemblerProjectWriter.EscapeAndQuoteString(ex.Token.Value));
                    break;
                case TokenType.CharLiteral:
                case TokenType.Message:
                    tw.Write(AssemblerProjectWriter.EscapeAndQuoteMessage(ex.Token.Value));
                    break;
                case TokenType.ArrayIndex:
                    tw.Write("[");
                    ToStringReal(ex.Subexpressions, tw);
                    tw.Write("]");
                    break;
                case TokenType.Assert:
                    tw.Write("assert (");
                    for (int i = 0; i < ex.Subexpressions.Count; i++)
                    {
                        if (i != 0)
                        {
                            tw.Write(", ");
                        }
                        ToStringReal(ex.Subexpressions[i], tw);
                    }
                    tw.Write(")");
                    break;
                case TokenType.For:
                    tw.Write("for (");
                    for (int i = 0; i < 3; i++)
                    {
                        if (i != 0)
                        {
                            tw.Write("; ");
                        }
                        var subex = ex.Subexpressions.GetOrNull(i);
                        ToStringReal(subex, tw);
                    }
                    tw.WriteLine(")");
                    ToStringReal(ex.Subexpressions.GetOrNull(3), tw);
                    break;
                case TokenType.While:
                    tw.Write("while (");
                    ToStringReal(ex.Subexpressions.GetOrNull(0), tw);
                    tw.WriteLine(")");
                    ToStringReal(ex.Subexpressions.GetOrNull(1), tw);
                    break;
                case TokenType.If:
                    tw.Write("if (");
                    ToStringReal(ex.Subexpressions.GetOrNull(0), tw);
                    tw.WriteLine(")");
                    ToStringReal(ex.Subexpressions.GetOrNull(1), tw);
                    {
                        var subex = ex.Subexpressions.GetOrNull(2);
                        if (subex != null)
                        {
                            ToStringReal(subex, tw);
                        }
                    }
                    break;
                case TokenType.Switch:
                    tw.Write("switch (");
                    ToStringReal(ex.Subexpressions.GetOrNull(0), tw);
                    tw.WriteLine(")");
                    ToStringReal(ex.Subexpressions.Skip(1), tw);
                    break;
                case TokenType.FunctionCall:
                    ToStringReal(ex.Subexpressions.GetOrNull(0), tw);
                    tw.Write("(");
                    ToStringReal(ex.Subexpressions.Skip(1), tw);
                    tw.Write(")");
                    break;
                case TokenType.PostDecrement:
                case TokenType.PostIncrement:
                    ToStringReal(ex.Subexpressions, tw);
                    tw.Write(ex.Token.Value);
                    break;

                //infix binary operators
                case TokenType.And:
                case TokenType.AndAssign:
                case TokenType.Assign:
                case TokenType.Colon:
                case TokenType.Comma:
                case TokenType.Divide:
                case TokenType.DivideAssign:
                case TokenType.Dot:
                case TokenType.EqualTo:
                case TokenType.LeftShift:
                case TokenType.LeftShiftAssign:
                case TokenType.LessThan:
                case TokenType.LessThanOrEqualTo:
                case TokenType.LogicalAnd:
                case TokenType.LogicalOr:
                case TokenType.Minus:
                case TokenType.MinusAssign:
                case TokenType.Modulo:
                case TokenType.ModuloAssign:
                case TokenType.Multiply:
                case TokenType.MultiplyAssign:
                case TokenType.NotEqualTo:
                case TokenType.Or:
                case TokenType.OrAssign:
                case TokenType.Plus:
                case TokenType.PlusAssign:
                case TokenType.QuestionMark:
                case TokenType.ReferenceAssign:
                case TokenType.ReferenceEqualTo:
                case TokenType.ReferenceNotEqualTo:
                case TokenType.ReferenceSwap:
                case TokenType.RightShift:
                case TokenType.RightShiftAssign:
                case TokenType.Xor:
                case TokenType.XorAssign:
                    //output left side
                    ToStringReal(ex.Subexpressions.GetOrNull(0), tw);

                    if (ex.TokenType != TokenType.Comma && ex.TokenType != TokenType.Dot)
                    {
                        tw.Write(" ");
                    }

                    if (String.IsNullOrEmpty(ex.Token.Value) || ex.Token.Value == "...")
                    {
                        if (keywordTableInverse.ContainsKey(ex.TokenType))
                        {
                            tw.Write(keywordTableInverse[ex.TokenType]);
                        }
                    }
                    else
                    {
                        tw.Write(ex.Token.Value);
                    }
                    if (ex.TokenType != TokenType.Dot)
                    {
                        tw.Write(" ");
                    }

                    ToStringReal(ex.Subexpressions.Skip(1), tw);
                    break;
            }



            //foreach (var ex in exBase.GetAllSubexpressionsRecursive())
            //{
            //    switch (ex.TokenType)
            //    {
            //        case TokenType.And:
            //            case TokenType.

            //        default:
            //            sb.Append(ex.Token.Value);
            //            break;
            //    }
            //}
            //return sb.ToString();
        }

        private IEnumerable<TokenExpression> GetAllSubexpressionsRecursive()
        {
            yield return this;
            foreach (var subexpression in this.Subexpressions)
            {
                if (subexpression != null)
                {
                    if (subexpression.Subexpressions.Count > 0)
                    {
                        foreach (var result in subexpression.GetAllSubexpressionsRecursive())
                        {
                            yield return result;
                        }
                    }
                    else
                    {
                        yield return subexpression;
                    }
                }
            }
        }

        public string ToStringReal()
        {
            return ToStringReal(this);
        }

        public TokenExpression Clone()
        {
            var clone = new TokenExpression(Token.Clone());
            clone.Subexpressions = this.Subexpressions.Clone(clone);
            clone.TokenType = this.TokenType;
            return clone;
        }

        public TokenExpression GetFirstSubexpression(TokenType tokenType)
        {
            return TokensWhere(this.Subexpressions, tokenType).FirstOrDefault();
        }

        public IEnumerable<TokenExpression> GetSubexpressionsRecursive(TokenType tokenType)
        {
            return TokensWhereRecursive(this, tokenType);
        }

        static IEnumerable<TokenExpression> TokensWhere(IEnumerable<TokenExpression> tokens, TokenType tokenType)
        {
            return tokens.Where(t => t != null && t.TokenType == tokenType);
        }

        static IEnumerable<TokenExpression> TokensWhereRecursive(TokenExpression token, TokenType tokenType)
        {
            if (token.TokenType == tokenType)
            {
                yield return token;
            }
            foreach (var subexpression in token.Subexpressions)
            {
                var whereRecursive = TokensWhereRecursive(subexpression, tokenType);
                foreach (var w in whereRecursive)
                {
                    yield return w;
                }
            }
        }

    }

    public class TokenExpressionCollection : Collection<TokenExpression>
    {
        TokenExpression Parent;
        public TokenExpressionCollection(TokenExpression parent)
            : base(new List<TokenExpression>())
        {
            this.Parent = parent;
        }

        public TokenExpressionCollection(TokenExpression parent, int initialCapacity)
            : base(new List<TokenExpression>(initialCapacity))
        {
            this.Parent = parent;
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
            {
                if (item != null)
                {
                    item.Parent = null;
                }
            }
            base.ClearItems();
        }

        protected override void InsertItem(int index, TokenExpression item)
        {
            if (item != null)
            {
                item.Parent = Parent;
            }
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            var oldItem = this.GetOrNull(index);
            if (oldItem != null)
            {
                oldItem.Parent = null;
            }
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TokenExpression item)
        {
            var oldItem = this.GetOrNull(index);
            if (oldItem != null)
            {
                oldItem.Parent = null;
            }
            if (item != null)
            {
                item.Parent = Parent;
            }
            base.SetItem(index, item);
        }

        public TokenExpressionCollection Clone(TokenExpression newParent)
        {
            var clone = new TokenExpressionCollection(newParent, this.Count);
            foreach (var item in this)
            {
                if (item == null)
                {
                    clone.Add(null);
                }
                else
                {
                    clone.Add(item.Clone());
                }
            }
            return clone;
        }
    }
}
