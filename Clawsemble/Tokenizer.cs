﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace Clawsemble
{
    public static class Tokenizer
    {
        public static List<Token> Tokenize(Stream Stream)
        {
            var reader = (TextReader)new StreamReader(Stream);
            var tokens = new List<Token>();

            var sb = new StringBuilder();
            var type = TokenType.Empty;

            uint line = 1;

            while (Stream.Position < Stream.Length) {
                char chr = (char)reader.Read();

                if ((type == TokenType.Comment && chr != '\r' && chr != '\n') ||
                    (type == TokenType.String && chr != '"' && chr != '\r' && chr != '\n') ||
                    (type == TokenType.CharacterEscape && sb.Length < 1) ||
                    (type == TokenType.Character && sb.Length < 1)) {
                    sb.Append(chr);
                    if (type == TokenType.CharacterEscape || type == TokenType.Character)
                        FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == '\n' || chr == '\r') {
                    if (type == TokenType.CharacterEscape || type == TokenType.Character) {
                        sb.Append(chr);
                        FinishToken(tokens, ref type, ref line, sb);
                    } else if (chr == '\r') {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.Break;
                    } else if (chr == '\n') {
                        if (type != TokenType.Break)
                            FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.Break;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else
                        throw new Exception("You shouldn't be here!");
                } else if (chr == ' ' || chr == '\t') {
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == ';') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Comment;
                } else if (chr == '\\') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.CharacterEscape;
                } else if (chr == '%') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Character;
                } else if (chr == '"') {
                    if (type == TokenType.String) {
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.String;
                    }
                } else if (chr == '.') {
                    if (type == TokenType.Empty)
                        type = TokenType.CompilerDirective;
                    else
                        type = TokenType.Error;
                } else if (chr == '#') {
                    if (type == TokenType.Empty)
                        type = TokenType.PreprocessorDirective;
                    else {
                        type = TokenType.Error;
                        FinishToken(tokens, ref type, ref line, sb);
                    }
                } else if (chr == '$') {
                    if (type != TokenType.HexadecimalEscape) {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.HexadecimalEscape;
                    } else {
                        type = TokenType.Error;
                        FinishToken(tokens, ref type, ref line, sb);
                    }
                } else if (chr == ':' || chr == ',') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Seperator;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == '!') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Not;
                } else if (chr == '[') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.ArrayOpen;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == ']') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.ArrayClose;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == '(') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.ParanthesisOpen;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == ')') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.ParanthesisClose;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == '<') {
                    if (type == TokenType.LessThan) {
                        type = TokenType.BitshiftLeft;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.LessThan;
                    }
                } else if (chr == '=') {
                    if (type == TokenType.Assign) {
                        type = TokenType.Equal;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else if (type == TokenType.Not) {
                        type = TokenType.NotEqual;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else if (type == TokenType.GreaterThan) {
                        type = TokenType.GreaterEqual;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else if (type == TokenType.LessThan) {
                        type = TokenType.LessEqual;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.Assign;
                    }
                } else if (chr == '>') {
                    if (type == TokenType.GreaterThan) {
                        type = TokenType.BitshiftRight;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.LessThan;
                    }
                } else if (chr == '&') {
                    if (type == TokenType.BitwiseAnd) {
                        type = TokenType.LogicalAnd;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.BitwiseAnd;
                        FinishToken(tokens, ref type, ref line, sb);
                    }
                } else if (chr == '|') {
                    if (type == TokenType.BitwiseOr) {
                        type = TokenType.LogicalOr;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.BitwiseOr;
                        FinishToken(tokens, ref type, ref line, sb);
                    }
                } else if (chr == '+') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Plus;
                } else if (chr == '-') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Minus;
                } else if (chr == '*') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.Multiply;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == '/') {
                    if (type == TokenType.Divide) {
                        type = TokenType.Modulo;
                        FinishToken(tokens, ref type, ref line, sb);
                    } else {
                        FinishToken(tokens, ref type, ref line, sb);
                        type = TokenType.Divide;
                    }
                } else if (chr == '^') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.BitwiseXOr;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr == '~') {
                    FinishToken(tokens, ref type, ref line, sb);
                    type = TokenType.BitwiseNot;
                    FinishToken(tokens, ref type, ref line, sb);
                } else if (chr >= '0' && chr <= '9') {
                    if (type == TokenType.Empty || type == TokenType.Minus || type == TokenType.Plus) {
                        if (type == TokenType.Plus)
                            sb.Append('-');
                        type = TokenType.Number;
                    }
                    sb.Append(chr);
                } else if (((chr >= 'A' && chr <= 'Z') || (chr >= 'a' && chr <= 'z') || chr == '_')
                           && type != TokenType.HexadecimalEscape && type != TokenType.Number) {
                    if (type == TokenType.Empty)
                        type = TokenType.Word;
                    sb.Append(chr);
                } else if (((chr >= 'A' && chr <= 'F') || (chr >= 'a' && chr <= 'f'))
                           && type == TokenType.HexadecimalEscape && type != TokenType.Number) {
                    sb.Append(chr);
                } else {
                    type = TokenType.Error;
                    sb.Append(chr);
                }
            }
				
            reader.Dispose();

            return tokens;
        }

        private static void FinishToken(List<Token> Tokens, ref TokenType Type, ref uint Line, StringBuilder Builder)
        {
            if (Builder.Length > 0) {
                if (Type == TokenType.Empty)
                    Type = TokenType.Error;
                Tokens.Add(new Token() { Type = Type, Content = Builder.ToString(), Line = Line, File = 0 });
                Builder.Clear();
            } else if (Type != TokenType.Empty) {
                Tokens.Add(new Token() { Type = Type, Line = Line, File = 0 });
                if (Type == TokenType.Break)
                    Line++;
            }
            Type = TokenType.Empty;
        }
    }
}

