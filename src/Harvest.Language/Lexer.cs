using System;
using System.Collections.Generic;
using System.Text;

namespace Harvest.Language
{
    internal sealed class LanguageError : Exception
    {
        internal Diagnostic Diagnostic { get; }
        internal LanguageError(string code, string message, SourceSpan span) { Diagnostic = new Diagnostic(code, message, span); }
    }
    internal sealed class Token
    {
        internal string Kind { get; }
        internal string Text { get; }
        internal SourceSpan Span { get; }
        internal Token(string kind, string text, SourceSpan span) { Kind = kind; Text = text; Span = span; }
    }
    internal static class Lexer
    {
        internal static List<Token> Scan(string source)
        {
            if (source.Length > RuntimeLimits.SourceBytes || Encoding.UTF8.GetByteCount(source) > RuntimeLimits.SourceBytes)
                throw new LanguageError("source_limit", "Source exceeds 64 KiB UTF-8.", new SourceSpan(0, 0, 1, 1));
            var tokens = new List<Token>(); var indents = new Stack<int>(); indents.Push(0);
            int offset = 0, line = 1;
            void Add(string kind, string value, int start, int length, int column)
            {
                if (tokens.Count >= RuntimeLimits.Tokens) throw new LanguageError("token_limit", "Too many tokens.", new SourceSpan(start, length, line, column));
                tokens.Add(new Token(kind, value, new SourceSpan(start, length, line, column)));
            }
            while (offset < source.Length)
            {
                int start = offset, end = start;
                while (end < source.Length && source[end] != '\n' && source[end] != '\r') end++;
                int i = start; while (i < end && source[i] == ' ') i++;
                if (i < end && source[i] == '\t') throw new LanguageError("invalid_indent", "Use spaces, not tabs.", new SourceSpan(i, 1, line, i - start + 1));
                if (i < end && source[i] != '#')
                {
                    int indent = i - start;
                    if (indent % 4 != 0) throw new LanguageError("invalid_indent", "Indentation must use four-space levels.", new SourceSpan(start, indent, line, 1));
                    if (indent > indents.Peek())
                    {
                        if (indent != indents.Peek() + 4 || indents.Count > RuntimeLimits.Depth) throw new LanguageError("nesting_limit", "Invalid or excessive indentation.", new SourceSpan(start, indent, line, 1));
                        indents.Push(indent); Add("indent", "", start, indent, 1);
                    }
                    while (indent < indents.Peek()) { indents.Pop(); Add("dedent", "", i, 0, indent + 1); }
                    if (indent != indents.Peek()) throw new LanguageError("invalid_indent", "Indentation does not match a prior level.", new SourceSpan(start, indent, line, 1));
                    while (i < end)
                    {
                        char c = source[i];
                        if (c == ' ') { i++; continue; }
                        if (c == '#') break;
                        int at = i, column = i - start + 1;
                        if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_')
                        {
                            i++; while (i < end && (char.IsLetterOrDigit(source[i]) && source[i] < 128 || source[i] == '_')) i++;
                            string word = source.Substring(at, i - at); Add("name", word, at, i - at, column); continue;
                        }
                        if (c >= '0' && c <= '9')
                        {
                            i++; while (i < end && source[i] >= '0' && source[i] <= '9') i++;
                            Add("number", source.Substring(at, i - at), at, i - at, column); continue;
                        }
                        if (c == '\'' || c == '"')
                        {
                            char quote = c; i++; var text = new StringBuilder(); bool closed = false;
                            while (i < end)
                            {
                                c = source[i++]; if (c == quote) { closed = true; break; }
                                if (c == '\\')
                                {
                                    if (i == end) break; c = source[i++];
                                    switch (c) { case 'n': c = '\n'; break; case 't': c = '\t'; break; case 'r': c = '\r'; break; case '\\': case '\'': case '"': break; default: throw new LanguageError("invalid_escape", "Unsupported string escape.", new SourceSpan(i - 2, 2, line, i - start - 1)); }
                                }
                                text.Append(c);
                                if (text.Length > RuntimeLimits.StringBytes) throw new LanguageError("string_limit", "String is too long.", new SourceSpan(at, i - at, line, column));
                            }
                            if (!closed) throw new LanguageError("unterminated_string", "Close the string on this line.", new SourceSpan(at, i - at, line, column));
                            string value = text.ToString(); if (Encoding.UTF8.GetByteCount(value) > RuntimeLimits.StringBytes) throw new LanguageError("string_limit", "String exceeds UTF-8 byte limit.", new SourceSpan(at, i - at, line, column));
                            Add("string", value, at, i - at, column); continue;
                        }
                        string op = c.ToString();
                        if (i + 1 < end) { string pair = source.Substring(i, 2); if (pair == "==" || pair == "!=" || pair == "<=" || pair == ">=" || pair == "//") op = pair; }
                        if ("+-*%<>=():,.".IndexOf(c) < 0 && op != "//" && op != "!=") throw new LanguageError("unsupported_syntax", "Unsupported character or syntax.", new SourceSpan(i, 1, line, column));
                        i += op.Length; Add(op, op, at, op.Length, column);
                    }
                    Add("newline", "", end, 0, end - start + 1);
                }
                offset = end;
                if (offset < source.Length && source[offset] == '\r') offset++;
                if (offset < source.Length && source[offset] == '\n') offset++;
                line++;
            }
            while (indents.Count > 1) { indents.Pop(); Add("dedent", "", offset, 0, 1); }
            Add("eof", "", offset, 0, 1); return tokens;
        }
    }
}
