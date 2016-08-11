﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Collections;

namespace Clawsemble
{
    public class Preprocessor
    {
        public List<Token> Tokens;
        public List<string> Files;
        public Dictionary<string, Constant> Defines;

        public Preprocessor()
        {
            Tokens = new List<Token>();
            Files = new List<string>();
            Defines = new Dictionary<string, Constant>();
            Defines.Add("NULL", new Constant(0));
        }

        public void Clear()
        {
            Tokens.Clear();
            Files.Clear();
            Defines.Clear();
            Defines.Add("NULL", new Constant(0));
        }

        public void DoFile(string Filename)
        {
            if (!File.Exists(Filename))
                throw new FileNotFoundException("Included file not found!", Filename);
            List<Token> ftokens = Tokenizer.Tokenize(File.OpenRead(Filename));
            
            string directive;
            var ifStack = new ArbitraryStack<bool>();
           
            Files.Add(Filename);

            for (int i = 0; i < ftokens.Count; i++) {
                if (ftokens[i].Type == TokenType.PreprocessorDirective) {
                    if (string.IsNullOrEmpty(ftokens[i].Content))
                        throw new CodeError(CodeErrorType.UnknownDirective, "Empty preprocessor directive!", ftokens[i], Filename);

                    directive = ftokens[i].Content.Trim().ToLower();
                    if (directive == "if" || directive == "elif" || directive == "elseif") {
                        if (directive == "elif" || directive == "elseif") {
                            if (ifStack.Count == 0)
                                throw new CodeError(CodeErrorType.IfMissmatched, "No preceeding opening if!", ftokens[i], Filename);
                            if (ifStack.Peek()) {
                                try {
                                    SkipAhead(ref i, ftokens, ifStack, true); // we have already successfully handled the if, skip to endif
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                                continue;
                            }
                        }

                        if (!IsBeforeEOF(i, ftokens.Count))
                            throw new CodeError(CodeErrorType.UnexpectedEOF, ftokens[i - 1].Line, Filename);
                        
                        if (ftokens[++i].Type == TokenType.ParanthesisOpen) {
                            Constant eval;
                            bool result;
                            try {
                                eval = EvaluateExpression(ref i, ftokens);
                            } catch (CodeError error) {
                                error.Filename = Filename;
                                throw error;
                            }

                            if (eval.Type == ConstantType.Numeric)
                                result = (bool)(eval.Number > 0);
                            else if (eval.Type == ConstantType.String)
                                result = (bool)(!string.IsNullOrEmpty(eval.String));
                            else
                                result = false;

                            if (result) {
                                if (directive == "if") {
                                    ifStack.Push(true); // success, handle what follows
                                } else if (directive == "elif" || directive == "elseif") {
                                    ifStack.Change(true);
                                }
                            } else {
                                if (directive == "if") {
                                    ifStack.Push(false); // no success, keep searching
                                } else if (directive == "elif" || directive == "elseif") {
                                    ifStack.Change(false); // no success, keep searching
                                }

                                try {
                                    SkipAhead(ref i, ftokens, ifStack, false); // skip to the next block
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                            }
                        } else
                            throw new CodeError(CodeErrorType.ExpectedExpression, ftokens[i], Filename);
                    } else if (directive == "ifdef" || directive == "ifndef" ||
                               directive == "elifdef" || directive == "elseifdef" || directive == "elifndef" || directive == "elseifndef") {
                        if (directive == "elifdef" || directive == "elseifdef" || directive == "elifndef" || directive == "elseifndef") {
                            if (ifStack.Count == 0)
                                throw new CodeError(CodeErrorType.IfMissmatched, "No preceeding opening if!", ftokens[i], Filename);
                            if (ifStack.Peek()) {
                                try {
                                    SkipAhead(ref i, ftokens, ifStack, true); // we have already successfully handled the if, skip to endif
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                                continue;
                            }
                        }
                        
                        if (ftokens[++i].Type == TokenType.Word) {
                            if (!string.IsNullOrEmpty(ftokens[i].Content)) {
                                if (Defines.ContainsKey(ftokens[i].Content.Trim())) { // the key exists
                                    if (directive == "ifdef") { // key exists AND we have an ifdef = success
                                        ifStack.Push(true); // add that if is solved
                                    } else if (directive == "ifndef") { // key exists AND we have an ifndef = no success
                                        ifStack.Push(false); // add that if is not solved and start search for an else
                                        try {
                                            SkipAhead(ref i, ftokens, ifStack, false); // and let's skip to the next interesting block
                                        } catch (CodeError error) {
                                            error.Filename = Filename;
                                            throw error;
                                        }
                                    } else if (directive == "elifdef" || directive == "elseifdef") { // we have a key and ELSEifdef
                                        ifStack.Change(true); // change if to solved
                                    } else if (directive == "elifndef" || directive == "elseifndef") {
                                        ifStack.Change(false); // no success; lets search for an else
                                        try {
                                            SkipAhead(ref i, ftokens, ifStack, false); // skip to the next interesting block
                                        } catch (CodeError error) {
                                            error.Filename = Filename;
                                            throw error;
                                        }
                                    }
                                } else {
                                    if (directive == "ifdef") { // key exists AND we have an ifdef = success
                                        ifStack.Push(false); // add that if is not solved and start search for an else
                                        try {
                                            SkipAhead(ref i, ftokens, ifStack, false); // and let's skip to the next interesting block
                                        } catch (CodeError error) {
                                            error.Filename = Filename;
                                            throw error;
                                        }
                                    } else if (directive == "ifndef") { // key exists AND we have an ifndef = no success
                                        ifStack.Push(true); // add it, as the if is solved
                                    } else if (directive == "elifdef" || directive == "elseifdef") { // we have a key and ELSEifdef
                                        ifStack.Change(false); // no success; lets search for an else
                                        try {
                                            SkipAhead(ref i, ftokens, ifStack, false); // skip to the next interesting block
                                        } catch (CodeError error) {
                                            error.Filename = Filename;
                                            throw error;
                                        }
                                    } else if (directive == "elifndef" || directive == "elseifndef") {
                                        ifStack.Change(true); // change if to solved
                                    }
                                }
                            } else
                                throw new CodeError(CodeErrorType.ConstantNotFound, "Constant name is empty!", ftokens[i]);
                        } else
                            throw new CodeError(CodeErrorType.ExpectedWord, ftokens[i]);
                    } else if (directive == "else") {
                        if (ifStack.Count == 0)
                            throw new CodeError(CodeErrorType.IfMissmatched, "No preceeding opening if!", ftokens[i], Filename);
                        if (ifStack.Peek()) {
                            try {
                                SkipAhead(ref i, ftokens, ifStack, true); // we have already successfully handled the if, skip to endif
                            } catch (CodeError error) {
                                error.Filename = Filename;
                                throw error;
                            }
                            continue;
                        }
                        ifStack.Change(true); // we assume nothing comes after the true block so we just jump into it
                    } else if (directive == "endif") {
                        if (ifStack.Count == 0)
                            throw new CodeError(CodeErrorType.IfMissmatched, "Too many closing if's!", ftokens[i], Filename);
                        ifStack.Pop(); // remove one if from the stack
                    } else if (directive == "inc" || directive == "include") {
                        if (IsBeforeEOF(i, ftokens.Count)) {
                            if (ftokens[i + 1].Type == TokenType.String) {
                                DoFile(ftokens[++i].Content);
                            } else
                                throw new CodeError(CodeErrorType.ExpectedString, ftokens[i], Filename);
                        } else
                            throw new CodeError(CodeErrorType.UnexpectedEOF, ftokens[i].Line, Filename);
                    } else if (directive == "err" || directive == "error") {
                        if (i + 1 < ftokens.Count) {
                            if (ftokens[i + 1].Type == TokenType.String) {
                                i++;
                                throw new CodeError(CodeErrorType.IntentionalError, ftokens[i].Content, ftokens[i].Line, Filename);
                            } else if (ftokens[i + 1].Type == TokenType.Number || ftokens[i + 1].Type == TokenType.Character ||
                                       ftokens[i + 1].Type == TokenType.CharacterRemove || ftokens[i + 1].Type == TokenType.HexadecimalEscape ||
                                       ftokens[i + 1].Type == TokenType.Word) {
                                Constant eval;
                                i++;
                                try {
                                    eval = EvaluateExpression(ref i, ftokens, 1);
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                                if (eval.Type == ConstantType.Numeric)
                                    throw new CodeError(CodeErrorType.IntentionalError, eval.Number.ToString(), ftokens[i].Line, Filename);
                                else if (eval.Type == ConstantType.String)
                                    throw new CodeError(CodeErrorType.IntentionalError, eval.String, ftokens[i].Line, Filename);
                                else
                                    throw new CodeError(CodeErrorType.ExpressionEmpty, ftokens[i].Line, Filename);
                            } else if (ftokens[i + 1].Type == TokenType.ParanthesisOpen) {
                                Constant eval;
                                i++;
                                try {
                                    eval = EvaluateExpression(ref i, ftokens);
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                                if (eval.Type == ConstantType.Numeric)
                                    throw new CodeError(CodeErrorType.IntentionalError, eval.Number.ToString(), ftokens[i].Line, Filename);
                                else if (eval.Type == ConstantType.String)
                                    throw new CodeError(CodeErrorType.IntentionalError, eval.String, ftokens[i].Line, Filename);
                                else
                                    throw new CodeError(CodeErrorType.ExpressionEmpty, ftokens[i].Line, Filename);
                            } else
                                throw new CodeError(CodeErrorType.IntentionalError, ftokens[i].Line, Filename);
                        } else
                            throw new CodeError(CodeErrorType.IntentionalError, ftokens[i].Line, Filename);
                    } else if (directive == "def" || directive == "define") {
                        string key = "";
                        Constant value = new Constant();

                        if (i + 2 < ftokens.Count) {
                            if (ftokens[i + 1].Type == TokenType.Word)
                                key = ftokens[++i].Content.Trim();
                            else
                                throw new CodeError(CodeErrorType.ExpectedWord, ftokens[i], Filename);
                            if (ftokens[i + 1].Type == TokenType.String ||
                                ftokens[i + 1].Type == TokenType.HexadecimalEscape ||
                                ftokens[i + 1].Type == TokenType.Character ||
                                ftokens[i + 1].Type == TokenType.Number ||
                                ftokens[i + 1].Type == TokenType.Word) {
                                i++;
                                try {
                                    value = EvaluateExpression(ref i, ftokens, 1);
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                            } else if (ftokens[i + 1].Type == TokenType.ParanthesisOpen) {
                                i++;
                                try {
                                    value = EvaluateExpression(ref i, ftokens);
                                } catch (CodeError error) {
                                    error.Filename = Filename;
                                    throw error;
                                }
                            }
                        } else if (IsBeforeEOF(i, ftokens.Count)) {
                            if (ftokens[i + 1].Type == TokenType.Word)
                                key = ftokens[++i].Content.Trim();
                            else
                                throw new CodeError(CodeErrorType.ExpectedWord, ftokens[i], Filename);
                        } else
                            throw new CodeError(CodeErrorType.UnexpectedEOF, ftokens[i].Line, Filename);
						
                        Defines.Add(key, value);
                    } else if (directive == "undef" || directive == "undefine") {
                        if (IsBeforeEOF(i, ftokens.Count)) {
                            if (ftokens[++i].Type == TokenType.Word) {
                                if (string.IsNullOrEmpty(ftokens[i].Content))
                                    throw new CodeError(CodeErrorType.ConstantNotFound, "Constant name is empty!", ftokens[i], Filename);
                                if (Defines.ContainsKey(ftokens[i].Content.Trim()))
                                    Defines.Remove(ftokens[i].Content.Trim());
                            } else
                                throw new CodeError(CodeErrorType.ExpectedWord, ftokens[i], Filename);
                        } else
                            throw new CodeError(CodeErrorType.UnexpectedEOF, ftokens[i].Line, Filename);
                    } else
                        throw new CodeError(CodeErrorType.UnknownDirective, ftokens[i], Filename);
                } else if (ftokens[i].Type == TokenType.ParanthesisOpen) { // we got an expression, nice
                    int origi = i;
                    Constant eval;
                    try {
                        eval = EvaluateExpression(ref i, ftokens);
                    } catch (CodeError error) {
                        error.Filename = Filename;
                        throw error;
                    }
                    if (eval.Type == ConstantType.Numeric) {
                        Tokens.Add(new Token() { Type = TokenType.Number, Content = eval.Number.ToString(),
                            Line = (uint)origi, File = (uint)Files.Count
                        });
                    } else if (eval.Type == ConstantType.String) {
                        Tokens.Add(new Token() { Type = TokenType.String, Content = eval.String,
                            Line = (uint)origi, File = (uint)Files.Count
                        });
                    } else
                        throw new CodeError(CodeErrorType.ExpressionEmpty, ftokens[i].Line, Filename);
                } else if (ftokens[i].Type == TokenType.Number || ftokens[i].Type == TokenType.Character ||
                           ftokens[i].Type == TokenType.HexadecimalEscape) {
                    Constant eval;
                    try {
                        eval = new Constant(ftokens[i]);
                    } catch (Exception ex) {
                        throw new CodeError(CodeErrorType.ConstantInvalid, ex.Message, ftokens[i], 0, ftokens[i].Position, Filename);
                    }

                    Tokens.Add(new Token() { Type = TokenType.Number, Content = eval.Number.ToString(),
                        Line = ftokens[i].Line, File = (uint)Files.Count
                    });
                } else if (ftokens[i].Type == TokenType.Word) {
                    if (Defines.ContainsKey(ftokens[i].Content)) {
                        Constant eval = Defines[ftokens[i].Content];

                        if (eval.Type == ConstantType.Numeric) {
                            Tokens.Add(new Token() { Type = TokenType.Number, Content = eval.Number.ToString(),
                                Line = ftokens[i].Line, File = (uint)Files.Count
                            });
                        } else if (eval.Type == ConstantType.String)
                            Tokens.Add(new Token() { Type = TokenType.String, Content = eval.String,
                                Line = ftokens[i].Line, File = (uint)Files.Count
                            });
                    } else { // if the word is not resolvable, add it again
                        Tokens.Add(new Token() { Type = ftokens[i].Type, Content = ftokens[i].Content,
                            Line = ftokens[i].Line, File = (uint)Files.Count
                        });
                    }
                } else if (ftokens[i].Type == TokenType.Break && Tokens.Count > 0) {
                    if (Tokens[Tokens.Count - 1].Type != TokenType.Break) {
                        Tokens.Add(new Token() { Type = ftokens[i].Type, Content = ftokens[i].Content,
                            Line = ftokens[i].Line, File = (uint)Files.Count
                        });
                    } // else drop it as we don't want newlines following each other
                } else if (IsOp(ftokens[i])) {
                    throw new CodeError(CodeErrorType.ExpressionUncontained, "Expressions need to be surrounded by parantheses!",
                        ftokens[i], Filename);
                } else if (ftokens[i].Type != TokenType.Comment && ftokens[i].Type != TokenType.CharacterRemove) {
                    // we cannot deal with the token just yet
                    Tokens.Add(new Token() { Type = ftokens[i].Type, Content = ftokens[i].Content,
                        Line = ftokens[i].Line, File = (uint)Files.Count
                    });
                }
            }

            if (ifStack.Count > 0)
                throw new CodeError(CodeErrorType.IfMissmatched, "Unterminated if's remaining!", Filename);
        }

        /* *
         * I assume this should skip until the next IF control statement of the same hierarchy
         * e.g.     IF      <= we are here and condition is not met
         *              IF
         *              ELIF
         *              ENDIF
         *          ELIF    <= we want to go here
         *          ...
         * */
        private void SkipAhead(ref int Pointer, List<Token> Tokens, ArbitraryStack<bool> IfStack, bool SkipToEnd)
        {
            int startDepth = IfStack.Count;
            string directive;

            for (; IsBeforeEOF(Pointer, Tokens.Count); Pointer++) {
                if (Tokens[Pointer].Type != TokenType.PreprocessorDirective)
                    continue;
                if (string.IsNullOrEmpty(Tokens[Pointer].Content))
                    throw new CodeError(CodeErrorType.UnknownDirective, "Empty preprocessor directive!", Tokens[Pointer]);
                
                directive = Tokens[Pointer].Content.Trim().ToLower();
                if (directive == "endif") {
                    if (IfStack.Count == 0)
                        throw new CodeError(CodeErrorType.IfMissmatched, "Too many closing if's!", Tokens[Pointer]);

                    IfStack.Pop(); // pop the placeholders again
                    if (IfStack.Count < startDepth) // as soon as we reach the end of the target level
                        return; // we hit something interesting
                } else if (directive == "if" || directive == "ifdef" || directive == "ifndef") {
                    IfStack.Push(true); // just adding a placeholder onto the if stack; these blocks are not parsed but skipped
                } else if (IfStack.Count == startDepth && !SkipToEnd &&
                           (directive == "elif" || directive == "elseif" || directive == "else" ||
                           directive == "elifdef" || directive == "elseifdef")) {
                    Pointer--;
                    return; // we hit something interesting
                }
            }

            throw new CodeError(CodeErrorType.UnexpectedEOF, Tokens[Pointer - 1].Line);
        }

        private bool IsBeforeEOF(int Pointer, int AvlLength, int ReqLength = 1)
        {
            return (bool)(Pointer + ReqLength < AvlLength);
        }

        private Constant EvaluateExpression(ref int Pointer, List<Token> Tokens, int MaxLength = int.MaxValue)
        {
            var valstack = new Stack<Constant>();
            var opstack = new Stack<Token>();
            int startPtr = Pointer;

            for (; Pointer - startPtr < MaxLength; Pointer++) {
                if (Tokens[Pointer].Type == TokenType.Break || Tokens[Pointer].Type == TokenType.Seperator ||
                    Tokens[Pointer].Type == TokenType.Comment) {
                    break;
                } else if (Tokens[Pointer].Type == TokenType.Number ||
                           Tokens[Pointer].Type == TokenType.Character ||
                           Tokens[Pointer].Type == TokenType.HexadecimalEscape ||
                           Tokens[Pointer].Type == TokenType.String) {
                    Constant con;
                    try {
                        con = new Constant(Tokens[Pointer]);
                    } catch (Exception ex) {
                        throw new CodeError(CodeErrorType.ConstantInvalid, ex.Message, Tokens[Pointer]);
                    }
                    valstack.Push(con);
                } else if (Tokens[Pointer].Type == TokenType.Word) {
                    if (Defines.ContainsKey(Tokens[Pointer].Content.Trim())) {
                        Constant cvar = Defines[Tokens[Pointer].Content.Trim()];
                        if (cvar.Type != ConstantType.Empty)
                            valstack.Push(cvar);
                        else
                            throw new CodeError(CodeErrorType.ConstantEmpty, Tokens[Pointer].Line);
                    } else
                        throw new CodeError(CodeErrorType.ConstantNotFound, Tokens[Pointer].Line);
                } else if (Tokens[Pointer].Type == TokenType.ParanthesisOpen) {
                    opstack.Push(Tokens[Pointer]);
                } else if (Tokens[Pointer].Type == TokenType.ParanthesisClose) {
                    while (opstack.Count > 0) {
                        if (opstack.Peek().Type == TokenType.ParanthesisOpen) {
                            opstack.Pop();
                            break;
                        } else {
                            Token optok = opstack.Pop();
                            try {
                                ExecOp(optok, valstack);
                            } catch (CodeError error) {
                                error.Token = optok;
                                throw error;
                            }
                        }
                    }
                } else if (IsOp(Tokens[Pointer])) {
                    Token stackop, currop = Tokens[Pointer];

                    while (opstack.Count > 0) {
                        stackop = opstack.Pop();

                        if (OpAssociativity(currop) == Associativity.LeftToRight && OpPrecedence(currop) <= OpPrecedence(stackop) ||
                            OpAssociativity(currop) == Associativity.RightToLeft && OpPrecedence(currop) < OpPrecedence(stackop)) {
                            try {
                                ExecOp(stackop, valstack);
                            } catch (CodeError error) {
                                error.Token = stackop;
                                throw error;
                            }
                        } else {
                            opstack.Push(stackop);
                            break;
                        }
                    }

                    opstack.Push(currop);
                } else
                    throw new CodeError(CodeErrorType.UnexpectedToken, Tokens[Pointer]);
            }

            if (opstack.Count > 0) {
                while (opstack.Count > 0) {
                    Token optok = opstack.Pop();
                    if (optok.Type == TokenType.ParanthesisOpen)
                        throw new CodeError(CodeErrorType.MissmatchedParantheses, Tokens[Pointer - 1].Line);
                    else if (!IsOp(optok))
                        throw new CodeError(CodeErrorType.ExpectedOperator, optok);
                    else {
                        try {
                            ExecOp(optok, valstack);
                        } catch (CodeError error) {
                            error.Token = optok;
                            throw error;
                        }
                    }
                }
            }
            if (valstack.Count > 1)
                throw new CodeError(CodeErrorType.ExpressionInvalid, Tokens[Pointer - 1].Line);
            else if (valstack.Count == 0)
                valstack.Push(new Constant(0));
            if (valstack.Peek().Type == ConstantType.Empty)
                throw new CodeError(CodeErrorType.ExpressionInvalid, "Expression evaluates to nothing!", Tokens[Pointer - 1].Line);

            return valstack.Pop(); // result can be a number or string
        }

        private void ExecOp(Token Token, Stack<Constant> Stack)
        {
            if (Token.Type == TokenType.BitwiseNot || Token.Type == TokenType.Not) {
                if (Stack.Count < 1)
                    throw new CodeError(CodeErrorType.StackUnderflow, "Too many operators!");
                Constant ct0 = Stack.Pop();

                if (Token.Type == TokenType.BitwiseNot)
                    Stack.Push(new Constant(~ct0.Number));
                else if (Token.Type == TokenType.Not)
                    Stack.Push(new Constant((ct0.Number > 0) ? 0 : 1));
                return;
            } else {
                if (Stack.Count < 2)
                    throw new CodeError(CodeErrorType.StackUnderflow, "Too many operators!");
                Constant ct0 = Stack.Pop(), ct1 = Stack.Pop();
                if (ct0.Type == ConstantType.String || ct1.Type == ConstantType.String) {
                    if (ct0.Type == ConstantType.Numeric)
                        ct0 = new Constant(ct0.Number.ToString());
                    else if (ct1.Type == ConstantType.Numeric)
                        ct1 = new Constant(ct1.Number.ToString());
                    
                    if (Token.Type == TokenType.Equal)
                        Stack.Push(new Constant((ct0.String == ct1.String) ? 1 : 0));
                    else if (Token.Type == TokenType.NotEqual)
                        Stack.Push(new Constant((ct0.String != ct1.String) ? 1 : 0));
                    else if (Token.Type == TokenType.Plus)
                        Stack.Push(new Constant(ct1.String + ct0.String));
                    else if (Token.Type == TokenType.Minus) {
                        if (ct1.String.EndsWith(ct0.String)) {
                            Stack.Push(new Constant(ct1.String.Substring(0, ct1.String.LastIndexOf(ct0.String))));
                        } else
                            Stack.Push(ct1);
                    } else if (Token.Type == TokenType.Divide)
                        Stack.Push(new Constant(ct1.String.Replace(ct0.String, "")));
                    else
                        throw new CodeError(CodeErrorType.OperationInvalid, "Can't apply arithmetic operation to type of string!");
                } else if (ct0.Type == ConstantType.Numeric && ct1.Type == ConstantType.Numeric) {
                    switch (Token.Type) {
                    case TokenType.BitshiftLeft:
                        Stack.Push(new Constant(ct1.Number << (int)ct0.Number));
                        return;
                    case TokenType.BitshiftRight:
                        Stack.Push(new Constant(ct1.Number >> (int)ct0.Number));
                        return;
                    case TokenType.BitwiseAnd:
                        Stack.Push(new Constant(ct1.Number & ct0.Number));
                        return;
                    case TokenType.BitwiseOr:
                        Stack.Push(new Constant(ct1.Number | ct0.Number));
                        return;
                    case TokenType.BitwiseXOr:
                        Stack.Push(new Constant(ct1.Number ^ ct0.Number));
                        return;
                    case TokenType.Divide:
                        if (ct0.Number == 0)
                            throw new CodeError(CodeErrorType.DivisionByZero);
                        Stack.Push(new Constant(ct1.Number / ct0.Number));
                        return;
                    case TokenType.GreaterEqual:
                        Stack.Push(new Constant((ct1.Number >= ct0.Number) ? 1 : 0));
                        return;
                    case TokenType.GreaterThan:
                        Stack.Push(new Constant((ct1.Number > ct0.Number) ? 1 : 0));
                        return;
                    case TokenType.LessEqual:
                        Stack.Push(new Constant((ct1.Number <= ct0.Number) ? 1 : 0));
                        return;
                    case TokenType.LessThan:
                        Stack.Push(new Constant((ct1.Number < ct0.Number) ? 1 : 0));
                        return;
                    case TokenType.LogicalAnd:
                        Stack.Push(new Constant(((ct1.Number > 0) && (ct0.Number > 0)) ? 1 : 0));
                        return;
                    case TokenType.LogicalOr:
                        Stack.Push(new Constant(((ct1.Number > 0) || (ct0.Number > 0)) ? 1 : 0));
                        return;
                    case TokenType.Minus:
                        Stack.Push(new Constant(ct1.Number - ct0.Number));
                        return;
                    case TokenType.Modulo:
                        if (ct0.Number == 0)
                            throw new CodeError(CodeErrorType.DivisionByZero);
                        Stack.Push(new Constant(ct1.Number % ct0.Number));
                        return;
                    case TokenType.Multiply:
                        Stack.Push(new Constant(ct1.Number * ct0.Number));
                        return;
                    case TokenType.NotEqual:
                        Stack.Push(new Constant((ct1.Number != ct0.Number) ? 1 : 0));
                        return;
                    case TokenType.Plus:
                        Stack.Push(new Constant(ct1.Number + ct0.Number));
                        return;
                    default:
                        return;
                    }
                } else
                    throw new CodeError(CodeErrorType.TypeMissmatch);
            }
        }

        private bool IsOp(Token Token)
        {
            return (OpPrecedence(Token) > 0);
        }

        private int OpPrecedence(Token Token)
        {
            if (Token.Type == TokenType.Plus || Token.Type == TokenType.Minus ||
                Token.Type == TokenType.Not || Token.Type == TokenType.BitwiseNot)
                return 1;
            if (Token.Type == TokenType.Multiply || Token.Type == TokenType.Divide ||
                Token.Type == TokenType.Modulo)
                return 2;
            if (Token.Type == TokenType.Plus || Token.Type == TokenType.Minus)
                return 3;
            if (Token.Type == TokenType.BitshiftLeft || Token.Type == TokenType.BitshiftRight)
                return 4;
            if (Token.Type == TokenType.GreaterThan || Token.Type == TokenType.LessThan ||
                Token.Type == TokenType.GreaterEqual || Token.Type == TokenType.LessEqual)
                return 5;
            if (Token.Type == TokenType.Equal || Token.Type == TokenType.NotEqual)
                return 6;
            if (Token.Type == TokenType.BitwiseAnd)
                return 7;
            if (Token.Type == TokenType.BitwiseXOr)
                return 8;
            if (Token.Type == TokenType.BitwiseOr)
                return 9;
            if (Token.Type == TokenType.LogicalAnd)
                return 10;
            if (Token.Type == TokenType.LogicalOr)
                return 11;
            return 0;
        }

        private enum Associativity
        {
            None,
            LeftToRight,
            RightToLeft
        }

        private Associativity OpAssociativity(Token Token)
        {
            if (Token.Type == TokenType.Not || Token.Type == TokenType.BitwiseNot) {
                return Associativity.RightToLeft;
            } else if (Token.Type == TokenType.Multiply || Token.Type == TokenType.Divide ||
                       Token.Type == TokenType.Modulo ||
                       Token.Type == TokenType.Plus || Token.Type == TokenType.Minus ||
                       Token.Type == TokenType.BitshiftLeft || Token.Type == TokenType.BitshiftRight ||
                       Token.Type == TokenType.GreaterThan || Token.Type == TokenType.LessThan ||
                       Token.Type == TokenType.GreaterEqual || Token.Type == TokenType.LessEqual ||
                       Token.Type == TokenType.Equal || Token.Type == TokenType.NotEqual ||
                       Token.Type == TokenType.BitwiseAnd || Token.Type == TokenType.BitwiseXOr ||
                       Token.Type == TokenType.BitwiseOr || Token.Type == TokenType.LogicalAnd ||
                       Token.Type == TokenType.LogicalOr ||
                       Token.Type == TokenType.Plus || Token.Type == TokenType.Minus) {
                return Associativity.LeftToRight;
            } else
                return Associativity.None;
        }
    }
}
