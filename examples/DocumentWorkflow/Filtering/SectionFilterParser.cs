using System.Globalization;
using DesignPatterns.TeachingProjects.DocumentWorkflow.Domain;

namespace DesignPatterns.TeachingProjects.DocumentWorkflow.Filtering;

/// <summary>
/// 把小型筛选语言解析成表达式树。
/// grammar:
/// expression := term (OR term)*
/// term       := factor (AND factor)*
/// factor     := NOT factor | '(' expression ')' | predicate
/// predicate  := audience '=' value | tag '=' value | pages '>=' number
/// </summary>
public sealed class SectionFilterParser
{
    private IReadOnlyList<string> _tokens = Array.Empty<string>();
    private int _position;

    public ISectionExpression Parse(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        _tokens = Tokenize(source);
        _position = 0;

        var expression = ParseExpression();
        if (!IsAtEnd)
        {
            throw Error($"未预期的标记 '{Peek()}'.");
        }

        return expression;
    }

    private bool IsAtEnd => _position >= _tokens.Count;

    private ISectionExpression ParseExpression()
    {
        var expression = ParseTerm();
        while (Match("OR"))
        {
            expression = new OrExpression(expression, ParseTerm());
        }

        return expression;
    }

    private ISectionExpression ParseTerm()
    {
        var expression = ParseFactor();
        while (Match("AND"))
        {
            expression = new AndExpression(expression, ParseFactor());
        }

        return expression;
    }

    private ISectionExpression ParseFactor()
    {
        if (Match("NOT"))
        {
            return new NotExpression(ParseFactor());
        }

        if (Match("("))
        {
            var expression = ParseExpression();
            Consume(")", "筛选表达式缺少右括号。");
            return expression;
        }

        return ParsePredicate();
    }

    private ISectionExpression ParsePredicate()
    {
        var field = ConsumeValue("需要字段名（audience、tag 或 pages）。");

        if (field.Equals("audience", StringComparison.OrdinalIgnoreCase))
        {
            Consume("=", "audience 后需要 '='。");
            var value = ConsumeValue("audience 需要 internal 或 external。");
            if (!Enum.TryParse<Audience>(value, ignoreCase: true, out var audience))
            {
                throw Error($"未知受众 '{value}'，只支持 internal 或 external。");
            }

            return new AudienceExpression(audience);
        }

        if (field.Equals("tag", StringComparison.OrdinalIgnoreCase))
        {
            Consume("=", "tag 后需要 '='。");
            return new TagExpression(ConsumeValue("tag 需要一个标签值。"));
        }

        if (field.Equals("pages", StringComparison.OrdinalIgnoreCase))
        {
            Consume(">=", "pages 后需要 '>='。");
            var value = ConsumeValue("pages 需要一个正整数。");
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var pages) || pages <= 0)
            {
                throw Error($"pages 值 '{value}' 不是正整数。");
            }

            return new MinimumPagesExpression(pages);
        }

        throw Error($"未知字段 '{field}'，只支持 audience、tag 或 pages。");
    }

    private bool Match(string expected)
    {
        if (IsAtEnd || !Peek().Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _position++;
        return true;
    }

    private void Consume(string expected, string message)
    {
        if (!Match(expected))
        {
            throw Error(message);
        }
    }

    private string ConsumeValue(string message)
    {
        if (IsAtEnd)
        {
            throw Error(message);
        }

        var value = Peek();
        if (value is "(" or ")" or "=" or ">=")
        {
            throw Error(message);
        }

        _position++;
        return value;
    }

    private string Peek() => _tokens[_position];

    private FormatException Error(string message) =>
        new($"筛选表达式第 {_position + 1} 个标记附近有误：{message}");

    private static IReadOnlyList<string> Tokenize(string source)
    {
        var tokens = new List<string>();
        var index = 0;

        while (index < source.Length)
        {
            if (char.IsWhiteSpace(source[index]))
            {
                index++;
                continue;
            }

            if (source[index] is '(' or ')' or '=')
            {
                tokens.Add(source[index].ToString());
                index++;
                continue;
            }

            if (source[index] == '>' && index + 1 < source.Length && source[index + 1] == '=')
            {
                tokens.Add(">=");
                index += 2;
                continue;
            }

            if (char.IsLetterOrDigit(source[index]) || source[index] is '-' or '_')
            {
                var start = index;
                while (index < source.Length &&
                       (char.IsLetterOrDigit(source[index]) || source[index] is '-' or '_'))
                {
                    index++;
                }

                tokens.Add(source[start..index]);
                continue;
            }

            throw new FormatException($"筛选表达式包含不支持的字符 '{source[index]}'（位置 {index + 1}）。");
        }

        return tokens.Count > 0
            ? tokens
            : throw new FormatException("筛选表达式不能为空。");
    }
}
