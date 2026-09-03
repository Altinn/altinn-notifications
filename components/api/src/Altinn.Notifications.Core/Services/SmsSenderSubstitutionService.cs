using System.Text.RegularExpressions;

using Altinn.Notifications.Core.Configuration;
using Altinn.Notifications.Core.Services.Interfaces;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Core.Services;

/// <inheritdoc cref="ISmsSenderSubstitutionService"/>
public class SmsSenderSubstitutionService : ISmsSenderSubstitutionService
{
    private readonly CompiledRule[] _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsSenderSubstitutionService"/> class.
    /// </summary>
    /// <param name="config">The sender substitution configuration.</param>
    public SmsSenderSubstitutionService(IOptions<SmsSenderSubstitutionConfig> config)
    {
        _rules = [.. config.Value.Rules
            .Where(r => !string.IsNullOrWhiteSpace(r.PhoneNumberPrefixPattern) && r.NumericSenderByServiceOwner.Count > 0)
            .Select(CompileRule)];
    }

    /// <inheritdoc/>
    public bool HasRules => _rules.Length > 0;

    /// <inheritdoc/>
    public string ResolveSender(string configuredSender, string recipientPhoneNumber, string serviceOwnerShortName)
    {
        if (_rules.Length == 0 || string.IsNullOrWhiteSpace(recipientPhoneNumber) || string.IsNullOrWhiteSpace(serviceOwnerShortName))
        {
            return configuredSender;
        }

        foreach (var rule in _rules)
        {
            if (!rule.Matches(recipientPhoneNumber))
            {
                continue;
            }

            if (rule.NumericSenderByServiceOwner.TryGetValue(serviceOwnerShortName, out var numericSender) && !string.IsNullOrWhiteSpace(numericSender))
            {
                return numericSender;
            }
        }

        return configuredSender;
    }

    /// <summary>
    /// Compiles a configured <see cref="SmsSenderSubstitutionRule"/> into a <see cref="CompiledRule"/>,
    /// using a fast literal-prefix comparison when the pattern is a simple anchored prefix,
    /// falling back to a compiled regular expression otherwise.
    /// </summary>
    private static CompiledRule CompileRule(SmsSenderSubstitutionRule rule)
    {
        var literalPrefix = TryExtractLiteralPrefix(rule.PhoneNumberPrefixPattern);

        return literalPrefix != null
            ? new CompiledRule(literalPrefix, null, rule.NumericSenderByServiceOwner)
            : new CompiledRule(null, new Regex(rule.PhoneNumberPrefixPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant), rule.NumericSenderByServiceOwner);
    }

    /// <summary>
    /// Attempts to extract a plain literal prefix from a pattern that contains no regex
    /// metacharacters other than a leading "^" anchor, so it can be matched using a fast
    /// ordinal string comparison instead of a full regex evaluation.
    /// </summary>
    /// <param name="pattern">The configured phone number prefix pattern.</param>
    /// <returns>The literal prefix if the pattern is a simple anchored literal; otherwise <c>null</c>.</returns>
    private static string? TryExtractLiteralPrefix(string pattern)
    {
        var candidate = pattern.StartsWith('^') ? pattern[1..] : pattern;

        if (candidate.Length == 0 || candidate.Any(IsRegexMetacharacter))
        {
            return null;
        }

        return candidate;
    }

    /// <summary>
    /// Determines whether a character is a regular expression metacharacter that would
    /// invalidate treating the pattern as a plain literal prefix.
    /// </summary>
    private static bool IsRegexMetacharacter(char c)
    {
        return c is '.' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|' or '\\' or '^' or '$';
    }

    /// <summary>
    /// A compiled substitution rule using either a fast literal prefix comparison or a
    /// compiled regular expression, along with the per-service-owner numeric sender map.
    /// </summary>
    private sealed class CompiledRule
    {
        private readonly string? _literalPrefix;
        private readonly Regex? _regex;

        public CompiledRule(string? literalPrefix, Regex? regex, Dictionary<string, string> numericSenderByServiceOwner)
        {
            _literalPrefix = literalPrefix;
            _regex = regex;
            NumericSenderByServiceOwner = numericSenderByServiceOwner;
        }

        public Dictionary<string, string> NumericSenderByServiceOwner { get; }

        public bool Matches(string phoneNumber)
        {
            return _literalPrefix != null
                ? phoneNumber.StartsWith(_literalPrefix, StringComparison.Ordinal)
                : _regex!.IsMatch(phoneNumber);
        }
    }
}
