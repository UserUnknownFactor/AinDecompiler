using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

namespace AinDecompiler.Compiler
{
    public partial class SecondPass
    {
        public SecondPass(Compiler parent)
        {
            this.Parent = parent;
        }

        Compiler Parent;


        class TokenReader : MyEnumerator2<TokenExpression>
        {
            public TokenReader(IList<TokenExpression> array)
                : base(array)
            {

            }

        }

        TokenReader reader;
        TokenType lastTokenDesired;
        TokenExpression firstTokenSeen;

        TokenExpression ReadToken(TokenType tokenType)
        {
            lastTokenDesired = tokenType;
            var token = reader.Peek();
            firstTokenSeen = token;

            if (token == null) return null;
            int startPosition = reader.Index;
        GoBack:
            ;
            switch (tokenType)
            {
                default:
                    {
                        if (token.TokenType != tokenType) goto leave;
                        reader.Read();
                        return token;
                    }
                    break;
                case TokenType.Statement:
                    {
                        switch (token.TokenType)
                        {
                            //check for keyword
                            case TokenType.Assert:
                            case TokenType.Message:
                            case TokenType.If:
                            case TokenType.Break:
                            case TokenType.Continue:
                            case TokenType.Jumps:
                            case TokenType.Return:
                            case TokenType.Goto:
                            case TokenType.Jump:
                            case TokenType.While:
                            case TokenType.Do:
                            case TokenType.For:
                            case TokenType.Switch:
                            case TokenType.Case:
                            case TokenType.Default:
                            case TokenType.Block:
                            case TokenType.Const:
                                {
                                    var result = ReadToken(token.TokenType);
                                    return result;
                                }
                                break;
                            //a data type token indicates a variable declaration
                            case TokenType.Ref:
                            case TokenType.Array:
                            case TokenType.Int:
                            case TokenType.Float:
                            case TokenType.String:
                            case TokenType.Void:
                            case TokenType.Lint:
                            case TokenType.Bool:
                            case TokenType.IMainSystem:
                                {
                                    var variableDeclaration = ReadToken(TokenType.VariableDeclaration);
                                    if (variableDeclaration == null)
                                    {
                                        //RaiseError("Error in variable declaration");
                                        goto leave;
                                    }
                                    return variableDeclaration;
                                }

                            //case TokenType.Ifdef:
                            //case TokenType.Endif:
                            //case TokenType.Assert:
                            case TokenType.Identifier:
                                //check for label or variable declaration
                                {
                                    var nextToken = reader.PeekAhead(1);
                                    if (nextToken.TokenType == TokenType.Colon)
                                    {
                                        var label = ReadToken(TokenType.Label);
                                        if (label != null) return label;
                                    }
                                    if (nextToken.TokenType == TokenType.OpenBrace && token.Token.Value == "do")
                                    {
                                        token.TokenType = TokenType.Do;
                                        goto GoBack;
                                    }

                                    if (nextToken.TokenType == TokenType.Identifier)
                                    {
                                        var variableDeclaration = ReadToken(TokenType.VariableDeclaration);
                                        if (variableDeclaration != null) return variableDeclaration;
                                    }
                                }
                                break;
                            case TokenType.OpenBrace:
                                //a block in the middle of code
                                {
                                    var block = ReadToken(TokenType.Block);
                                    if (block == null) goto leave;
                                    return block;
                                }
                        }

                        if (token.TokenType == TokenType.Semicolon)
                        {
                            reader.Read();
                            return new TokenExpression(TokenType.Statement);
                        }

                        //check for expression and semicolon
                        var expression = ReadToken(TokenType.Expression);
                        if (expression == null)
                        {
                            RaiseError("Unable to parse statement text as an expression");
                            goto leave;
                        }
                        var semicolon = ReadToken(TokenType.Semicolon);
                        if (semicolon == null)
                        {
                            if (!ignoreMissingSemicolon)
                            {
                                RaiseError("Missing semicolon after statement");
                                goto leave;
                            }
                        }
                        return expression;
                    }
                    break;
                case TokenType.Expression:
                    {
                        var expression = ReadExpression();
                        if (expression == null) goto leave;
                        return expression;
                    }
                    break;
                case TokenType.Const:
                    {
                        var constToken = reader.Read();
                        var variableDeclaration = ReadToken(TokenType.VariableDeclaration);
                        if (variableDeclaration == null)
                        {
                            RaiseError("Error in variable declaration after 'const'");
                            goto leave;
                        }
                        constToken.Subexpressions.Add(variableDeclaration);
                        return constToken;
                    }
                    break;
                case TokenType.VariableDeclaration:
                    {
                        var variableDeclarationToken = new TokenExpression("VariableDeclaration");
                        variableDeclarationToken.TokenType = TokenType.VariableDeclaration;

                        var dataType = ReadToken(TokenType.DataType);
                        if (dataType == null) goto leave;

                        variableDeclarationToken.Subexpressions.Add(dataType);

                        while (true)
                        {
                            var identifier = ReadToken(TokenType.Identifier);
                            if (identifier == null)
                            {
                                RaiseError("Variable name is not a valid identifier.  It must be a word, not a keyword, number, or operator.");
                                goto leave;
                            }

                            var separator = reader.Read();
                            if (separator == null) goto endofinput;

                            if (separator.TokenType == TokenType.OpenBracket)
                            {
                                var dataTypeReal = Parent.GetDataType(dataType);
                                if (dataTypeReal == null)
                                {
                                    RaiseError("Unable to parse the variables data type (array declaration)");
                                    goto leave;
                                }
                                if (!dataTypeReal.DataType.IsArray())
                                {
                                    RaiseError("Cannot declare array dimensions to something that is not an array");
                                    goto leave;
                                }
                                int totalArrayDimensions = dataTypeReal.ArrayDimensions;
                                int arrayDimensionsRemaining = totalArrayDimensions;
                                List<int> arraySizes = new List<int>();

                                while (true)
                                {
                                    arrayDimensionsRemaining--;
                                    if (arrayDimensionsRemaining < 0)
                                    {
                                        RaiseError("Too many array dimensions specified");
                                        goto leave;
                                    }

                                    //must be a number, or a const int declared in the global scope
                                    TokenExpression expressionToken = ReadToken(TokenType.Expression);
                                    InitialValue initialValue = this.EvaluateConstExpression(expressionToken, DataType.Int, true);
                                    if (initialValue != null && initialValue.DataType == DataType.Int)
                                    {
                                        arraySizes.Add(initialValue.IntValue);
                                    }
                                    else
                                    {
                                        RaiseError("Unable to parse array element count as a constant");
                                    }

                                    separator = ReadToken(TokenType.CloseBracket);
                                    if (separator == null)
                                    {
                                        RaiseError("Missing closed bracket after array element count");
                                        goto leave;
                                    }
                                    separator = reader.Read();
                                    if (separator == null)
                                    {
                                        RaiseError("Unexpeced end of input");
                                        goto leave;
                                    }
                                    if (separator.TokenType == TokenType.OpenBracket)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                if (arrayDimensionsRemaining != 0)
                                {
                                    RaiseError("Not enough array dimensions specified");
                                    goto leave;
                                }
                                var arrayBoundsDeclarationToken = new TokenExpression(TokenType.ArrayBoundsDeclaration);
                                foreach (var arraySize in arraySizes)
                                {
                                    arrayBoundsDeclarationToken.Subexpressions.Add(new TokenExpression(arraySize.ToString()));
                                }
                                identifier.Subexpressions.Add(arrayBoundsDeclarationToken);
                                variableDeclarationToken.Subexpressions.Add(identifier);
                            }
                            else if (separator.TokenType == TokenType.Assign)
                            {
                                //var nextToken = reader.Peek();  //deleteme
                                var assignToken = separator;
                                bool oldCommaOkay = _commaOkay;
                                _commaOkay = false;
                                var expression = ReadToken(TokenType.Expression);
                                if (expression == null)
                                {
                                    RaiseError("Unable to parse the variable's initial value as an expression");
                                    goto leave;
                                }
                                _commaOkay = oldCommaOkay;

                                assignToken.Subexpressions.Add(identifier);
                                assignToken.Subexpressions.Add(expression);
                                variableDeclarationToken.Subexpressions.Add(assignToken);

                                separator = reader.Read();
                                if (separator == null) goto endofinput;
                            }
                            else
                            {
                                variableDeclarationToken.Subexpressions.Add(identifier);
                            }

                            if (separator.TokenType == TokenType.Comma)
                            {
                                //var comma = separator;
                            }
                            else if (separator.TokenType == TokenType.Semicolon)
                            {
                                //var semicolon = separator;
                                break;
                            }
                            else
                            {
                                if (!ignoreMissingSemicolon)
                                {
                                    RaiseError("Missing comma or semicolon at end of variable declaration", -1);
                                    goto leave;
                                }
                            }
                        }

                        return variableDeclarationToken;
                    }
                    break;
                case TokenType.DataType:
                    {
                        var dataType = new TokenExpression(TokenType.DataType);
                        if (token.TokenType == TokenType.Ref)
                        {
                            dataType.Subexpressions.Add(reader.Read());
                            token = reader.Peek();
                            if (token == null) goto endofinput;
                        }
                        if (token.TokenType == TokenType.Array)
                        {
                            dataType.Subexpressions.Add(reader.Read());
                            var at = ReadToken(TokenType.At);
                            if (at == null)
                            {
                                RaiseError("Missing '@' after 'array'");
                                goto leave;
                            }
                            var arrayType = reader.Read();
                            if (arrayType == null) goto endofinput;
                            if (!(arrayType.IsDataType() || arrayType.TokenType == TokenType.Identifier)) goto leave;
                            dataType.Subexpressions.Add(arrayType);
                            at = reader.Peek();
                            if (at != null && at.TokenType == TokenType.At)
                            {
                                reader.Read();
                                var dimensions = ReadToken(TokenType.Number);
                                if (dimensions == null)
                                {
                                    RaiseError("Array dimensions must be a number");
                                    goto leave;
                                }
                                dataType.Subexpressions.Add(dimensions);
                            }
                        }
                        else
                        {
                            token = reader.Read();
                            if (!(token.IsDataType() || token.TokenType == TokenType.Identifier))
                            {
                                RaiseError("Data type name is not a built-in type or valid identifier.", -1);
                                goto leave;
                            }
                            dataType.Subexpressions.Add(token);
                        }
                        return dataType;
                    }
                    break;
                case TokenType.Label:
                    {
                        var identifier = ReadToken(TokenType.Identifier);
                        if (identifier == null)
                        {
                            RaiseError("The label name is an invalid identifier, it can't be a keyword, operator, or number.");
                            goto leave;
                        }
                        var colon = ReadToken(TokenType.Colon);
                        if (colon == null)
                        {
                            RaiseError("Missing colon after label name.");
                            goto leave;
                        }
                        var labelToken = new TokenExpression(TokenType.Label);
                        labelToken.Subexpressions.Add(identifier);
                        return labelToken;
                    }
                    break;
                //case TokenType.ExpressionAndSemicolon:
                //    {
                //        //expression may be null - used by for loop and return statement
                //        var expression = ReadToken(TokenType.Expression);
                //        var semicolon = ReadToken(TokenType.Semicolon);
                //        if (semicolon == null) goto leave;
                //        if (expression == null) return semicolon;
                //        return expression;
                //    }
                //    break;
                case TokenType.BlockOrSingleStatement:
                    {
                        if (token.TokenType == TokenType.OpenBrace)
                        {
                            var block = ReadToken(TokenType.Block);
                            return block;
                        }
                        var expression = ReadToken(TokenType.Statement);
                        if (expression == null)
                        {
                            SkipStatement();
                            expression = new TokenExpression();
                        }
                        return expression;
                    }
                    break;
                case TokenType.Block:
                    {
                        bool containsError = false;
                        var blockToken = new TokenExpression("...");
                        blockToken.TokenType = TokenType.Block;
                        // { statements }
                        var openBrace = ReadToken(TokenType.OpenBrace);
                        if (openBrace == null) return null;
                        while (true)
                        {
                            token = reader.Peek();
                            if (token == null) goto endofinput;
                            if (token.TokenType == TokenType.CloseBrace)
                            {
                                reader.Read();
                                break;
                            }
                            var statement = ReadToken(TokenType.Statement);
                            if (statement == null)
                            {
                                SkipStatement();
                            }
                            blockToken.Subexpressions.Add(statement);
                        }
                        return blockToken;
                    }
                    break;


                case TokenType.Message:
                    {
                        var message = reader.Read();
                        return message;
                    }
                    break;
                case TokenType.If:
                    {
                        var newToken = reader.Read().Clone();
                        var openParenthesis = ReadToken(TokenType.OpenParenthesis);
                        if (openParenthesis == null)
                        {
                            RaiseError("'if' keyword is not followed by an open parenthesis");
                            goto leave;
                        }
                        var expression = ReadToken(TokenType.Expression);
                        if (expression == null)
                        {
                            RaiseError("Failed to parse the 'if' condition as an expression");
                            goto leave;
                        }
                        var closedParenthesis = ReadToken(TokenType.ClosedParenthesis);
                        if (closedParenthesis == null)
                        {
                            RaiseError("'if' condition is not followed by a closed parenthesis");
                            goto leave;
                        }
                        var ifBody = ReadToken(TokenType.BlockOrSingleStatement);
                        if (ifBody == null) goto leave;
                        var elseToken = ReadToken(TokenType.Else);
                        TokenExpression elseBody = null;
                        if (elseToken != null)
                        {
                            elseBody = ReadToken(TokenType.BlockOrSingleStatement);
                            if (elseBody == null)
                            {
                                RaiseError("Failed to parse 'else' as a block or statement.");
                                goto leave;
                            }
                        }
                        newToken.Subexpressions.Add(expression);
                        newToken.Subexpressions.Add(ifBody);
                        if (elseBody != null) newToken.Subexpressions.Add(elseBody);
                        return newToken;
                    }
                    break;
                case TokenType.Break:
                case TokenType.Continue:
                    {
                        var statementToken = reader.Read();
                        var semicolon = ReadToken(TokenType.Semicolon);
                        if (semicolon == null)
                        {
                            if (!ignoreMissingSemicolon)
                            {
                                RaiseError("Missing semicolon after " + tokenType.ToString() + " keyword.");
                                goto leave;
                            }
                        }
                        return statementToken;
                    }
                    break;
                case TokenType.Jumps:
                case TokenType.Return:
                    {
                        var returnToken = reader.Read().Clone();
                        var nextToken = reader.Peek();
                        if (nextToken == null) goto endofinput;
                        if (nextToken.TokenType == TokenType.Semicolon && tokenType == TokenType.Return)
                        {
                            nextToken = reader.Read();
                            return returnToken;
                        }
                        var expression = ReadToken(TokenType.Expression);
                        if (expression == null)
                        {
                            RaiseError("Unable to parse the " + tokenType.ToString() + " value as an expression");
                            goto leave;
                        }
                        var semicolon = ReadToken(TokenType.Semicolon);
                        if (semicolon == null)
                        {
                            if (!ignoreMissingSemicolon)
                            {
                                RaiseError("Missing semicolon after " + tokenType.ToString());
                                goto leave;
                            }
                        }
                        returnToken.Subexpressions.Add(expression);
                        return returnToken;
                    }
                    break;
                case TokenType.Goto:
                case TokenType.Jump:
                    {
                        var gotoToken = reader.Read().Clone();
                        var labelName = ReadToken(TokenType.Identifier);
                        if (labelName == null)
                        {
                            RaiseError("The name of the label is an invalid identifier, it must not be named the same as a keyword or operator, and must not be a number.");
                            goto leave;
                        }
                        var semicolon = ReadToken(TokenType.Semicolon);
                        if (semicolon == null)
                        {
                            if (!ignoreMissingSemicolon)
                            {
                                RaiseError("Missing semicolon after " + tokenType.ToString());
                                goto leave;
                            }
                        }
                        gotoToken.Subexpressions.Add(labelName);
                        return gotoToken;
                    }
                    break;
                case TokenType.While:
                    {
                        var whileToken = reader.Read().Clone();
                        var openParenthesisToken = ReadToken(TokenType.OpenParenthesis);
                        if (openParenthesisToken == null)
                        {
                            RaiseError("Missing open parenthesis after 'while' keyword.");
                            goto leave;
                        }
                        var conditionExpression = ReadToken(TokenType.Expression);
                        if (conditionExpression == null)
                        {
                            RaiseError("Failed to parse 'while' condition as an expression.");
                            goto leave;
                        }
                        var closedParenthesisToken = ReadToken(TokenType.ClosedParenthesis);
                        if (closedParenthesisToken == null)
                        {
                            RaiseError("Missing closed parenthesis after 'while' condition.");
                            goto leave;
                        }
                        var loopBody = ReadToken(TokenType.BlockOrSingleStatement);
                        if (loopBody == null)
                        {
                            RaiseError("Failed to parse loop body as a statement or block");
                            goto leave;
                        }
                        whileToken.Subexpressions.Add(conditionExpression);
                        whileToken.Subexpressions.Add(loopBody);
                        return whileToken;
                    }
                    break;
                case TokenType.Do:
                    {
                        var doWhileToken = reader.Read().Clone();
                        var loopBody = ReadToken(TokenType.BlockOrSingleStatement);
                        if (loopBody == null)
                        {
                            RaiseError("Failed to parse loop body as a statement or block.");
                            goto leave;
                        }
                        var whileToken = reader.Peek();
                        if (whileToken == null || whileToken.TokenType != TokenType.While)
                        {
                            RaiseError("Missing 'while' after a 'do' block.");
                            goto leave;
                        }
                        whileToken = reader.Read();
                        var openParenthesisToken = ReadToken(TokenType.OpenParenthesis);
                        if (openParenthesisToken == null)
                        {
                            RaiseError("Missing open parenthesis after 'while' keyword.");
                            goto leave;
                        }
                        var conditionExpression = ReadToken(TokenType.Expression);
                        if (conditionExpression == null)
                        {
                            RaiseError("Failed to parse 'while' condition as an expression.");
                            goto leave;
                        }
                        var closedParenthesisToken = ReadToken(TokenType.ClosedParenthesis);
                        if (closedParenthesisToken == null)
                        {
                            RaiseError("Missing closed parenthesis after 'while' condition.");
                            goto leave;
                        }
                        var semicolonToken = ReadToken(TokenType.Semicolon);
                        if (semicolonToken == null)
                        {
                            RaiseError("Missing semicolon after 'while' condition.");
                            goto leave;
                        }

                        doWhileToken.Subexpressions.Add(conditionExpression);
                        doWhileToken.Subexpressions.Add(loopBody);
                        return doWhileToken;
                    }
                    break;
                case TokenType.For:
                    {
                        var forToken = reader.Read().Clone();
                        var openParenthesisToken = ReadToken(TokenType.OpenParenthesis);
                        if (openParenthesisToken == null)
                        {
                            RaiseError("Missing open parenthesis after 'for' keyword");
                            goto leave;
                        }
                        TokenExpression initExpression = null;
                        TokenExpression condExpression = null;
                        TokenExpression incExpression = null;
                        TokenExpression separator;

                        separator = ReadToken(TokenType.Semicolon);
                        if (separator == null)
                        {
                            initExpression = ReadToken(TokenType.Expression);
                            if (initExpression == null)
                            {
                                RaiseError("Failed to parse 'for' initializer as an expression (do not declare variables in the declaration of a 'for' loop)");
                                goto leave;
                            }
                            separator = ReadToken(TokenType.Semicolon);
                            if (separator == null)
                            {
                                RaiseError("Missing semicolon after 'for' initializer.");
                                goto leave;
                            }
                        }
                        separator = ReadToken(TokenType.Semicolon);
                        if (separator == null)
                        {
                            condExpression = ReadToken(TokenType.Expression);
                            if (condExpression == null)
                            {
                                RaiseError("Failed to parse 'for' loop condition as an expression");
                                goto leave;
                            }
                            separator = ReadToken(TokenType.Semicolon);
                            if (separator == null)
                            {
                                RaiseError("Missing semicolon after 'for' loop condition");
                                goto leave;
                            }
                        }
                        separator = ReadToken(TokenType.ClosedParenthesis);
                        if (separator == null)
                        {
                            incExpression = ReadToken(TokenType.Expression);
                            if (incExpression == null)
                            {
                                RaiseError("Failed to parse 'for' increment as an expression");
                                goto leave;
                            }
                            separator = ReadToken(TokenType.ClosedParenthesis);
                            if (separator == null)
                            {
                                RaiseError("Missing closed parenthesis after 'for' increment");
                                goto leave;
                            }
                        }

                        var loopBody = ReadToken(TokenType.BlockOrSingleStatement);
                        if (loopBody == null)
                        {
                            RaiseError("Failed to parse 'for' loop body as a block or statement");
                            goto leave;
                        }

                        forToken.Subexpressions.Add(initExpression);
                        forToken.Subexpressions.Add(condExpression);
                        forToken.Subexpressions.Add(incExpression);
                        forToken.Subexpressions.Add(loopBody);
                        return forToken;
                    }
                    break;
                case TokenType.Switch:
                    {
                        var switchToken = reader.Read().Clone();
                        var openParenthesisToken = ReadToken(TokenType.OpenParenthesis);
                        if (openParenthesisToken == null)
                        {
                            RaiseError("Missing open parenethsis after 'switch' keyword");
                            goto leave;
                        }
                        var expression = ReadToken(TokenType.Expression);
                        if (expression == null)
                        {
                            RaiseError("Failed to parse 'switch' expression as an expression");
                            goto leave;
                        }
                        var closedParenthesisToken = ReadToken(TokenType.ClosedParenthesis);
                        if (closedParenthesisToken == null)
                        {
                            RaiseError("Missing closed parenthesis after 'switch' expression");
                            goto leave;
                        }
                        var block = ReadToken(TokenType.Block);
                        if (block == null)
                        {
                            RaiseError("Failed to parse 'switch' statement block");
                            goto leave;
                        }
                        switchToken.Subexpressions.Add(expression);
                        switchToken.Subexpressions.Add(block);
                        return switchToken;
                    }
                    break;
                case TokenType.Case:
                    {
                        var caseToken = reader.Read().Clone();
                        var expression = ReadToken(TokenType.Expression);
                        if (expression == null)
                        {
                            RaiseError("Failed to parse switch case value as an expression");
                            goto leave;
                        }
                        var colon = ReadToken(TokenType.Colon);
                        if (colon == null)
                        {
                            RaiseError("Missing colon after switch case value");
                            goto leave;
                        }
                        caseToken.Subexpressions.Add(expression);
                        return caseToken;
                    }
                    break;
                case TokenType.Assert:
                    {
                        var assertToken = reader.Read();
                        if (assertToken == null) goto endofinput;
                        var openParenthesis = ReadToken(TokenType.OpenParenthesis);
                        if (openParenthesis == null)
                        {
                            RaiseError("Missing open parenthesis after 'assert' keyword");
                            goto leave;
                        }
                        var expression = ReadToken(TokenType.Expression);
                        if (expression == null)
                        {
                            RaiseError("Failed to parse 'assert' value as an expression");
                            goto leave;
                        }
                        var closedParenthesis = ReadToken(TokenType.ClosedParenthesis);
                        if (closedParenthesis == null)
                        {
                            RaiseError("Missing closed parenthesis after 'assert' expression");
                            goto leave;
                        }
                        var semicolon = ReadToken(TokenType.Semicolon);
                        if (semicolon == null)
                        {
                            if (!ignoreMissingSemicolon)
                            {
                                RaiseError("Missing semicolon after 'assert' statement");
                                goto leave;
                            }
                        }

                        assertToken = assertToken.Clone();
                        assertToken.Subexpressions.Add(expression);
                        return assertToken;
                    }
                    break;
                case TokenType.Default:
                    {
                        var defaultToken = reader.Read();
                        var colon = ReadToken(TokenType.Colon);
                        if (colon == null)
                        {
                            RaiseError("Missing colon after 'default' case label");
                            goto leave;
                        }
                        return defaultToken;
                    }
                    break;
            }




            reader.Index = startPosition;
            return null;
        endofinput:
            RaiseError("Unexpected end of input", -1);
        leave:
            reader.Index = startPosition;
            return null;
        }

        private void SkipStatement()
        {
            //skip ahead for ; or end of block, passing by ()
            int bracesCount = 0;
            int parenthesisCount = 0;
            var token = reader.Read();
            while (token != null)
            {
                if (token.TokenType == TokenType.Semicolon)
                {
                    if (bracesCount <= 0 && parenthesisCount <= 0)
                    {
                        break;
                    }
                }
                else if (token.TokenType == TokenType.OpenBrace)
                {
                    bracesCount++;
                }
                else if (token.TokenType == TokenType.OpenParenthesis)
                {
                    parenthesisCount++;
                }
                else if (token.TokenType == TokenType.CloseBrace)
                {
                    bracesCount--;
                    if (bracesCount <= 0) break;
                }
                else if (token.TokenType == TokenType.ClosedParenthesis)
                {
                    parenthesisCount--;
                }
                token = reader.Read();
            }
        }

        public List<string> Errors = new List<string>();
        public event EventHandler<ErrorEventArgs> Error;

        string lastErrorFilename = null;

        private void RaiseError(string errorMessage, int tokenDisplacement)
        {
            var token = reader.PeekAhead(tokenDisplacement);
            int row = -1, column = -1;
            string filename = null;
            if (token != null)
            {
                row = token.Row;
                column = token.Column;
                filename = token.FileName;
                errorMessage = "Line " + (row + 1).ToString() + ", column " + (column + 1).ToString() + ": " + errorMessage;
            }
            if (Error != null)
            {
                if (filename != lastErrorFilename)
                {
                    if (filename != null)
                    {
                        //Error(this, new ErrorEventArgs(new Exception("In file: " + filename)));
                        errorMessage = "In file: " + filename + Environment.NewLine + errorMessage;
                    }
                }
                if (filename != null)
                {
                    lastErrorFilename = filename;
                }
                Error(this, new ErrorEventArgs(new Exception(errorMessage)));
            }
            Errors.Add(errorMessage);
        }

        private void RaiseError(string errorMessage)
        {
            RaiseError(errorMessage, 0);
        }

        static Dictionary<TokenType, int> InfixOperatorPrecedence = GetInfixOperatorPrecdence();
        static Dictionary<TokenType, int> PrefixOperatorPrecedence = GetPrefixOperatorPrecedence();
        static Dictionary<TokenType, int> PostfixOperatorPrecedence = GetPostfixOperatorPrecedence();
        static HashSet<TokenType> RightAssociativeOperators = GetRightAssociativeOperators();

        private static HashSet<TokenType> GetRightAssociativeOperators()
        {
            var set = new HashSet<TokenType>();

            set.Add(TokenType.Assign);
            set.Add(TokenType.PlusAssign);
            set.Add(TokenType.MinusAssign);
            set.Add(TokenType.MultiplyAssign);
            set.Add(TokenType.DivideAssign);
            set.Add(TokenType.ModuloAssign);
            set.Add(TokenType.LeftShiftAssign);
            set.Add(TokenType.RightShiftAssign);
            set.Add(TokenType.AndAssign);
            set.Add(TokenType.XorAssign);
            set.Add(TokenType.OrAssign);
            set.Add(TokenType.QuestionMark);
            set.Add(TokenType.Colon);
            set.Add(TokenType.ReferenceAssign);
            set.Add(TokenType.ReferenceSwap);
            set.Add(TokenType.Comma);
            return set;
        }

        private static Dictionary<TokenType, int> GetPostfixOperatorPrecedence()
        {
            var dic = new Dictionary<TokenType, int>();
            dic.Add(TokenType.OpenParenthesis, 32);
            dic.Add(TokenType.OpenBracket, 32);
            dic.Add(TokenType.Increment, 32);
            dic.Add(TokenType.Decrement, 32);
            return dic;
        }

        private static Dictionary<TokenType, int> GetPrefixOperatorPrecedence()
        {
            var dic = new Dictionary<TokenType, int>();
            dic.Add(TokenType.Increment, 32);
            dic.Add(TokenType.Decrement, 32);
            dic.Add(TokenType.Not, 30);
            dic.Add(TokenType.Complement, 30);
            dic.Add(TokenType.Minus, 28); //negative
            dic.Add(TokenType.Plus, 28); //positive
            dic.Add(TokenType.And, 28); //'address of' a function
            dic.Add(TokenType.New, 28);

            dic.Add(TokenType.Comma, 2);  //empty comma expression (for function argument lists)
            return dic;
        }

        private static Dictionary<TokenType, int> GetInfixOperatorPrecdence()
        {
            var dic = new Dictionary<TokenType, int>();
            dic.Add(TokenType.Dot, 32);
            dic.Add(TokenType.Multiply, 24);
            dic.Add(TokenType.Divide, 24);
            dic.Add(TokenType.Modulo, 24);
            dic.Add(TokenType.Plus, 22);
            dic.Add(TokenType.Minus, 22);
            dic.Add(TokenType.LeftShift, 20);
            dic.Add(TokenType.RightShift, 20);
            dic.Add(TokenType.LessThan, 18);
            dic.Add(TokenType.LessThanOrEqualTo, 18);
            dic.Add(TokenType.GreaterThan, 18);
            dic.Add(TokenType.GreaterThanOrEqualTo, 18);
            dic.Add(TokenType.EqualTo, 16);
            dic.Add(TokenType.NotEqualTo, 16);
            dic.Add(TokenType.ReferenceEqualTo, 16);
            dic.Add(TokenType.ReferenceNotEqualTo, 16);
            dic.Add(TokenType.And, 14);
            dic.Add(TokenType.Xor, 12);
            dic.Add(TokenType.Or, 10);
            dic.Add(TokenType.LogicalAnd, 8);
            dic.Add(TokenType.LogicalOr, 6);
            dic.Add(TokenType.Assign, 4);
            dic.Add(TokenType.PlusAssign, 4);
            dic.Add(TokenType.MinusAssign, 4);
            dic.Add(TokenType.MultiplyAssign, 4);
            dic.Add(TokenType.DivideAssign, 4);
            dic.Add(TokenType.ModuloAssign, 4);
            dic.Add(TokenType.LeftShiftAssign, 4);
            dic.Add(TokenType.RightShiftAssign, 4);
            dic.Add(TokenType.AndAssign, 4);
            dic.Add(TokenType.XorAssign, 4);
            dic.Add(TokenType.OrAssign, 4);
            dic.Add(TokenType.QuestionMark, 4);
            dic.Add(TokenType.Colon, 4);
            dic.Add(TokenType.ReferenceAssign, 4);
            dic.Add(TokenType.ReferenceSwap, 4);
            dic.Add(TokenType.Comma, 2);
            return dic;
        }

        bool _colonOkay = false;
        bool _commaOkay = true;

        private TokenExpression ReadExpression()
        {
            bool oldColonOkey = _colonOkay;
            var expr = ReadExpression(0);
            _colonOkay = oldColonOkey;
            return expr;
        }

        bool IsPrefixOperator(TokenType tokenType)
        {
            return PrefixOperatorPrecedence.ContainsKey(tokenType) && !(!_commaOkay && tokenType == TokenType.Comma);
        }
        private bool IsInfixOperator(TokenType tokenType)
        {
            return InfixOperatorPrecedence.ContainsKey(tokenType) && !(!_commaOkay && tokenType == TokenType.Comma) && !(!_colonOkay && tokenType == TokenType.Colon);
        }

        private TokenExpression ReadExpression(int minimumPrecedence)
        {
            //TODO: Fix ? : operator (make : not work by itself)

            var token = reader.Peek();
            if (token == null) return null;
            var tokenType = token.TokenType;

            int currentOperatorPrecedence = -1;

            TokenExpression expression = null;

            if (tokenType == TokenType.OpenParenthesis)
            {
                var parenthesisToken = reader.Read();
                expression = ReadExpression();
                if (expression == null) return null;
                var closedParenthesisToken = ReadToken(TokenType.ClosedParenthesis);
                if (closedParenthesisToken == null) return null;

                token = reader.Peek();
            }
            else if (IsPrefixOperator(tokenType))
            {
                var prefixOperator = reader.Read().Clone();
                currentOperatorPrecedence = PrefixOperatorPrecedence[tokenType];

                if (tokenType == TokenType.Increment) prefixOperator.TokenType = TokenType.PreIncrement;
                if (tokenType == TokenType.Decrement) prefixOperator.TokenType = TokenType.PreDecrement;
                if (tokenType == TokenType.Minus) prefixOperator.TokenType = TokenType.Negative;
                if (tokenType == TokenType.Plus) prefixOperator.TokenType = TokenType.Positive;
                if (tokenType == TokenType.Comma) prefixOperator.Subexpressions.Add(null);
                if (tokenType == TokenType.And) prefixOperator.TokenType = TokenType.AddressOf;

                expression = prefixOperator;
                var nextExpression = ReadExpression(currentOperatorPrecedence);
                if (nextExpression == null) return null;
                expression.Subexpressions.Add(nextExpression);

                token = reader.Peek();
            }
            else
            {
                //check for a symbol or literal
                if (tokenType == TokenType.Identifier ||
                    tokenType == TokenType.Number ||
                    tokenType == TokenType.StringLiteral ||
                    tokenType == TokenType.Message ||
                    tokenType == TokenType.Int ||
                    tokenType == TokenType.String ||
                    tokenType == TokenType.Float)
                {
                    token = reader.Read();

                    if (tokenType == TokenType.Message)
                    {
                        token = token.Clone();
                        token.TokenType = TokenType.CharLiteral;
                        tokenType = TokenType.CharLiteral;
                    }
                    expression = token;
                }
                else
                {
                    return null;
                }

                token = reader.Peek();
            }

            //allow end of input
            if (token == null)
            {
                return expression;
            }
            tokenType = token.TokenType;

        continueParsing:

            currentOperatorPrecedence = -1;

            if (PostfixOperatorPrecedence.ContainsKey(tokenType))
            {
                currentOperatorPrecedence = PostfixOperatorPrecedence[tokenType];
                if (currentOperatorPrecedence >= minimumPrecedence)
                {
                    //check for parenthesis or braces
                    if (tokenType == TokenType.OpenParenthesis)
                    {
                        bool oldCommaOkay = _commaOkay;
                        _commaOkay = true;

                        var functionCall = reader.Read().Clone();
                        functionCall.TokenType = TokenType.FunctionCall;

                        var innerExpression = ReadExpression();
                        _commaOkay = oldCommaOkay;

                        //innerExpression may be null
                        var closedParenthesis = ReadToken(TokenType.ClosedParenthesis);
                        if (closedParenthesis == null) return null;

                        functionCall.Subexpressions.Add(expression);
                        functionCall.Subexpressions.Add(innerExpression);
                        expression = functionCall;
                    }
                    else if (tokenType == TokenType.OpenBracket)
                    {
                        var arrayIndex = reader.Read().Clone();
                        arrayIndex.TokenType = TokenType.ArrayIndex;
                        var innerExpression = ReadExpression();
                        if (innerExpression == null) return null;
                        var closedBracket = ReadToken(TokenType.CloseBracket);
                        if (closedBracket == null) return null;

                        arrayIndex.Subexpressions.Add(expression);
                        arrayIndex.Subexpressions.Add(innerExpression);
                        expression = arrayIndex;
                    }
                    else
                    {
                        var postfixOperator = reader.Read().Clone();
                        if (postfixOperator.TokenType == TokenType.Increment) postfixOperator.TokenType = TokenType.PostIncrement;
                        if (postfixOperator.TokenType == TokenType.Decrement) postfixOperator.TokenType = TokenType.PostDecrement;

                        postfixOperator.Subexpressions.Add(expression);
                        expression = postfixOperator;
                    }
                    token = reader.Peek();
                    if (token == null) return expression;
                    tokenType = token.TokenType;
                }
            }

            if (IsInfixOperator(tokenType))
            {
                currentOperatorPrecedence = InfixOperatorPrecedence[tokenType];
                if (currentOperatorPrecedence >= minimumPrecedence)
                {
                    if (tokenType == TokenType.QuestionMark)
                    {
                        _colonOkay = true;
                    }
                    //else if (tokenType == TokenType.Colon)
                    //{
                    //    if (_colonOkay)
                    //    {

                    //    }
                    //    else
                    //    {
                    //        return expression;
                    //    }
                    //}

                    var infixOperator = reader.Read().Clone();
                    bool isRightAssociative = RightAssociativeOperators.Contains(tokenType);
                    var nextExpression = ReadExpression(currentOperatorPrecedence + (isRightAssociative ? 0 : 1));

                    if (tokenType == TokenType.QuestionMark)
                    {
                        _colonOkay = false;
                    }

                    if (nextExpression == null) return null;

                    infixOperator.Subexpressions.Add(expression);
                    infixOperator.Subexpressions.Add(nextExpression);
                    expression = infixOperator;

                    token = reader.Peek();
                    if (token == null) return expression;
                    tokenType = token.TokenType;
                }
            }

            if (currentOperatorPrecedence == -1)
            {
                return expression;
            }

            if (currentOperatorPrecedence >= minimumPrecedence && (IsInfixOperator(tokenType) || PostfixOperatorPrecedence.ContainsKey(tokenType)))
            {
                goto continueParsing;
            }

            return expression;
        }

        Dictionary<Function, TokenExpression> FunctionDefinitionTokens
        {
            get
            {
                return this.Parent.FunctionDefinitionTokens;
            }
        }

        Dictionary<Function, TokenExpression> FunctionDeclarationTokens
        {
            get
            {
                return this.Parent.FunctionDeclarationTokens;
            }
        }

        Dictionary<string, IVariable> Symbols
        {
            get
            {
                return this.Parent.Symbols;
            }
        }

        bool ignoreMissingSemicolon = false;

        public void CompileSecondPass(Function function, TokenExpression token)
        {
            var tokens = token.Subexpressions;
            var definition = CompileTokens(tokens);
            token.Subexpressions.Clear();
            token.Subexpressions.AddRange(definition);
        }

        public void CompileSecondPass()
        {
            foreach (var pair in this.FunctionDefinitionTokens)
            {
                var function = pair.Key;
                var token = pair.Value;
                CompileSecondPass(function, token);
            }
        }

        public List<TokenExpression> CompileTokens(IList<TokenExpression> tokens)
        {
            var oldReader = this.reader;
            this.reader = new TokenReader(tokens);

            List<TokenExpression> definition = new List<TokenExpression>();
            while (true)
            {
                var nextToken = reader.Peek();
                if (nextToken == null)
                {
                    break;
                }
                var statement = ReadToken(TokenType.Statement);
                if (statement == null)
                {
                    try
                    {
                        this.ignoreMissingSemicolon = true;
                        statement = ReadToken(TokenType.Statement);
                    }
                    finally
                    {
                        this.ignoreMissingSemicolon = false;
                    }
                }

                if (statement != null)
                {
                    definition.Add(statement);
                }
                else
                {
                    SkipStatement();
                }
            }
            this.reader = oldReader;
            return definition;
        }
    }
}
