using System;
using System.Collections.Generic;
using System.Globalization;

namespace Harvest.Language
{
    internal enum Op { Push, PushArithmetic, Load, Store, Pop, Unary, Binary, Field, Call, Jump, JumpFalse, ShortAnd, ShortOr, RangeStart, RangeNext }
    internal sealed class Instruction
    {
        internal Op Op; internal object? Arg; internal int Target; internal SourceSpan Span;
        internal Instruction(Op op, object? arg, SourceSpan span, int target = 0) { Op = op; Arg = arg; Span = span; Target = target; }
    }
    public sealed class CompiledProgram
    {
        internal Instruction[] Code { get; }
        internal RuntimeLimits Limits { get; }
        internal CompiledProgram(Instruction[] code, RuntimeLimits limits) { Code = code; Limits = limits; }
        public Execution Start(long actorId) => new Execution(this, actorId);
    }
    public sealed class CompileResult
    {
        public CompiledProgram? Program { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        internal CompileResult(CompiledProgram? program, Diagnostic? error) { Program = program; Diagnostics = error == null ? Array.Empty<Diagnostic>() : Array.AsReadOnly(new[] { error }); }
    }
    public static class LanguageCompiler
    {
        public static CompileResult Compile(string source, Capabilities capabilities, RuntimeLimits? limits = null)
        {
            try { return new CompileResult(new Parser(Lexer.Scan(source), capabilities).Parse(limits ?? new RuntimeLimits()), null); }
            catch (LanguageError e) { return new CompileResult(null, e.Diagnostic); }
        }
    }
    internal sealed class Parser
    {
        private readonly List<Token> tokens; private readonly Capabilities capabilities; private readonly List<Instruction> code = new List<Instruction>();
        private int cursor, nodes, depth, loopId;
        internal Parser(List<Token> tokens, Capabilities capabilities) { this.tokens = tokens; this.capabilities = capabilities; }
        private Token Current => tokens[cursor];
        private bool Is(string value)
        {
            switch (value)
            {
                case "if": case "else": case "while": case "for": case "in": case "range":
                    return Current.Kind == "name" && Current.Text == value;
                default: return Current.Kind == value;
            }
        }
        private Token Take() => tokens[cursor++];
        private bool Match(string value) { if (!Is(value)) return false; Take(); return true; }
        private Token Need(string value) { if (!Is(value)) Error("unexpected_token", "Expected " + value + "."); return Take(); }
        private void Error(string code, string message) => throw new LanguageError(code, message, Current.Span);
        private void Gate(string name, SourceSpan span) { if (!capabilities.Allows(name)) throw new LanguageError("capability_locked", "Capability is locked: " + name, span); }
        private int Emit(Op op, object? arg, SourceSpan span, int target = 0)
        { if (++nodes > RuntimeLimits.Nodes) throw new LanguageError("node_limit", "Program is too complex.", span); code.Add(new Instruction(op, arg, span, target)); return code.Count - 1; }
        private void Enter() { if (++depth > RuntimeLimits.Depth) Error("nesting_limit", "Expression or suite nesting is too deep."); }
        internal CompiledProgram Parse(RuntimeLimits limits)
        { while (!Is("eof")) Statement(); return new CompiledProgram(code.ToArray(), limits); }
        private void Suite()
        { Enter(); Need(":"); Need("newline"); Need("indent"); while (!Is("dedent") && !Is("eof")) Statement(); Need("dedent"); depth--; }
        private void Statement()
        {
            Token first = Current;
            if (Match("if"))
            {
                Gate("conditions", first.Span); Expression(); int branch = Emit(Op.JumpFalse, "conditions", first.Span); Suite();
                if (Match("else")) { int end = Emit(Op.Jump, null, first.Span); code[branch].Target = code.Count; Suite(); code[end].Target = code.Count; }
                else code[branch].Target = code.Count; return;
            }
            if (Match("while"))
            {
                Gate("while", first.Span); int begin = code.Count; Expression(); int end = Emit(Op.JumpFalse, "while", first.Span); Suite(); Emit(Op.Jump, "while", first.Span, begin); code[end].Target = code.Count; return;
            }
            if (Match("for"))
            {
                Gate("for", first.Span); Token name = Variable(); Need("in"); Token range = Need("range"); Gate("range", range.Span); Need("("); Expression(); Need(")");
                string key = "$loop" + loopId++; int setup = Emit(Op.RangeStart, new[] { key, name.Text }, first.Span); int body = code.Count; Suite(); Emit(Op.RangeNext, new[] { key, name.Text }, first.Span, body); code[setup].Target = code.Count; return;
            }
            if (Current.Kind == "name" && cursor + 1 < tokens.Count && tokens[cursor + 1].Kind == "=")
            { Gate("assignment", first.Span); Token name = Variable(); Need("="); Expression(); Emit(Op.Store, name.Text, name.Span); }
            else { Expression(); Emit(Op.Pop, null, first.Span); }
            Need("newline");
        }
        private Token Variable()
        {
            Token token = Need("name");
            if (token.Text == "True" || token.Text == "False" || token.Text == "if" || token.Text == "else" || token.Text == "while" || token.Text == "for" || token.Text == "in" || token.Text == "and" || token.Text == "or" || token.Text == "not" || token.Text == "def" || token.Text == "import" || token.Text == "return" || token.Text == "class")
                throw new LanguageError("unsupported_syntax", "Reserved name cannot be assigned.", token.Span);
            return token;
        }
        private void Expression(int min = 0)
        {
            Enter(); Prefix();
            while (true)
            {
                string op = Operator(Current); int precedence = Precedence(op); if (precedence < min) break;
                Token token = Take(); Gate(op == "and" || op == "or" ? "conditions" : "arithmetic", token.Span);
                if (op == "and" || op == "or")
                { int jump = Emit(op == "and" ? Op.ShortAnd : Op.ShortOr, null, token.Span); Expression(precedence + 1); Emit(Op.Unary, "bool", token.Span); code[jump].Target = code.Count; }
                else { Expression(precedence + 1); Emit(Op.Binary, op, token.Span); }
                if (precedence == 3 && Precedence(Operator(Current)) == 3) Error("unsupported_syntax", "Chained comparisons are unsupported; use and.");
            }
            depth--;
        }
        private static int Precedence(string op)
        { switch (op) { case "or": return 0; case "and": return 1; case "==": case "!=": case "<": case "<=": case ">": case ">=": return 3; case "+": case "-": return 4; case "*": case "//": case "%": return 5; default: return -1; } }
        private static string Operator(Token token)
            => token.Kind == "name" ? (token.Text == "and" || token.Text == "or" ? token.Text : "") : token.Kind;
        private void Prefix()
        {
            Token token = Take();
            if ((token.Kind == "name" && token.Text == "not") || token.Kind == "-" || token.Kind == "+")
            {
                Gate(token.Text == "not" ? "conditions" : "arithmetic", token.Span);
                if (token.Kind == "-" && Current.Kind == "number" && Current.Text == "9223372036854775808") { Take(); Emit(Op.PushArithmetic, long.MinValue, token.Span); }
                else { Expression(token.Text == "not" ? 2 : 6); Emit(Op.Unary, token.Text, token.Span); }
            }
            else if (token.Kind == "number")
            { if (!long.TryParse(token.Text, NumberStyles.None, CultureInfo.InvariantCulture, out long value)) throw new LanguageError("numeric_overflow", "Integer is outside int64 range.", token.Span); Emit(Op.Push, value, token.Span); }
            else if (token.Kind == "string") Emit(Op.Push, token.Text, token.Span);
            else if (token.Kind == "name" && (token.Text == "True" || token.Text == "False")) Emit(Op.Push, token.Text == "True", token.Span);
            else if (token.Kind == "(") { Expression(); Need(")"); }
            else if (token.Kind == "name")
            {
                if (token.Text == "def" || token.Text == "class" || token.Text == "import" || token.Text == "return") throw new LanguageError("unsupported_syntax", "Construct is unsupported in this language subset.", token.Span);
                if (Match("("))
                {
                    if (!IsApi(token.Text)) throw new LanguageError("unsupported_api", "Unknown API: " + token.Text, token.Span);
                    Gate(token.Text, token.Span); int count = 0;
                    if (!Is(")")) { Expression(); count++; while (Match(",")) { Expression(); count++; if (count > 1) Error("argument_count", "APIs accept at most one argument."); } }
                    Need(")"); int expected = token.Text == "wait" || token.Text == "log" ? 1 : 0;
                    if (count != expected) throw new LanguageError("argument_count", "Incorrect API argument count.", token.Span);
                    Emit(Op.Call, token.Text, token.Span);
                }
                else Emit(Op.Load, token.Text, token.Span);
            }
            else throw new LanguageError("unsupported_syntax", "Expected a supported expression.", token.Span);
            while (Match("."))
            {
                Token field = Need("name");
                if (field.Text != "ok" && field.Text != "reason" && field.Text != "x" && field.Text != "y" && field.Text != "heading" && field.Text != "ripe" && field.Text != "crop" && field.Text != "fuel" && field.Text != "cargo" && field.Text != "implement")
                    throw new LanguageError("unsupported_member", "Unknown immutable record field.", field.Span);
                Emit(Op.Field, field.Text, field.Span);
            }
        }
        private static bool IsApi(string name)
        { switch (name) { case "move_forward": case "turn_left": case "turn_right": case "harvest": case "unload": case "refuel": case "sense": case "position": case "fuel": case "cargo": case "wait": case "log": return true; default: return false; } }
    }
}
