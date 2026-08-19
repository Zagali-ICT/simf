// Tests: SIMF.Api.Tests/GridColumnsTests.cs
using System.Linq.Expressions;
using System.Reflection;

namespace SIMF.Common.Grids;

/// <summary>
/// Builds the free-text search predicate: one OR chain over the searchable string
/// columns, as a single expression tree, so it translates to one SQL WHERE clause
/// and never pulls rows into memory.
///
/// <para>
/// The chain cannot be assembled by OR-ing the declared lambdas directly: each was
/// written with its own <c>ParameterExpression</c>, and a lambda body only binds to
/// the parameter instance it was compiled against. Every body is therefore rebound
/// onto one shared parameter before the bodies are combined.
/// </para>
///
/// <para>
/// <c>string.Contains</c> rather than <c>EF.Functions.Like</c>: <c>EF.Functions</c>
/// lives in <c>Microsoft.EntityFrameworkCore</c>, which this project does not
/// reference, and the hand-written services being replaced all built
/// <c>Like(col, $"%{term}%")</c> with no escaping — so an operator typing <c>%</c>
/// matched every row and <c>_</c> matched any character. <c>Contains</c> has no
/// pattern language, so that defect cannot recur. Neither form is SARGable and
/// case-insensitivity comes from the column collation either way, so there is no
/// index regression.
/// </para>
/// </summary>
internal static class GridSearchPredicate
{
    private static readonly MethodInfo StringContains =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly ConstantExpression NullString =
        Expression.Constant(null, typeof(string));

    /// <summary>
    /// Produces <c>row =&gt; (row.A != null &amp;&amp; row.A.Contains(term)) ||
    /// (row.B != null &amp;&amp; row.B.Contains(term))</c>: a single server-side
    /// substring test per column, ORed, against one shared parameter. Null when
    /// there is nothing to search.
    /// </summary>
    internal static Expression<Func<TEntity, bool>>? Build<TEntity>(
        IReadOnlyList<Expression<Func<TEntity, string?>>> selectors, string? search)
    {
        if (selectors.Count == 0 || string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var row = Expression.Parameter(typeof(TEntity), "row");

        // One box for the whole chain, so the OR branches share ONE SQL parameter
        // instead of emitting the same term N times. Two boxes, because an Arabic
        // column is matched against the FOLDED needle and every other column
        // against the raw one - folding a Latin needle would be a no-op, but it
        // would still cost a second parameter and a second set of REPLACEs.
        var trimmed = search.Trim();
        var term = TermExpression(trimmed);
        MemberExpression? foldedTerm = null;

        Expression? matched = null;
        foreach (var selector in selectors)
        {
            var body = ParameterReplacer.Rebind(selector.Body, selector.Parameters[0], row);
            Expression test;
            if (GridArabicFold.Applies(selector.Body))
            {
                foldedTerm ??= TermExpression(GridArabicFold.Fold(trimmed));
                test = Match(GridArabicFold.FoldColumn(body), foldedTerm);
            }
            else
            {
                test = Match(body, term);
            }
            matched = matched is null ? test : Expression.OrElse(matched, test);
        }

        return Expression.Lambda<Func<TEntity, bool>>(matched!, row);
    }

    /// <summary>The per-column contains used by the string filter path, so search
    /// and filter cannot drift apart in their matching semantics.</summary>
    internal static Expression<Func<TEntity, bool>> Contains<TEntity>(
        Expression<Func<TEntity, string?>> selector, string term)
    {
        var trimmed = term.Trim();
        // The same fold the search chain applies, for the same reason: a filter box
        // over an Arabic column has to find the row whichever way the name was
        // typed, and search and filter must not disagree about what matches.
        var body = GridArabicFold.Applies(selector.Body)
            ? GridArabicFold.FoldColumn(selector.Body)
            : selector.Body;
        var needle = GridArabicFold.Applies(selector.Body)
            ? GridArabicFold.Fold(trimmed)
            : trimmed;
        return Expression.Lambda<Func<TEntity, bool>>(
            Match(body, TermExpression(needle)),
            selector.Parameters);
    }

    // Nullable reference annotations are erased at runtime, so a nullable and a
    // non-nullable string column are indistinguishable here and every body is
    // null-guarded. EF Core's null-semantics pass prunes the guard when it knows
    // the column is NOT NULL, so the redundant case costs nothing in SQL.
    private static Expression Match(Expression body, Expression term) =>
        Expression.AndAlso(
            Expression.NotEqual(NullGuardTarget(body), NullString),
            Expression.Call(body, StringContains, term));

    /// <summary>The column the null guard should test.
    ///
    /// <para>When the body is a fold, the guard has to reach past the REPLACE chain
    /// to the column underneath. Testing the chain itself would still be correct at
    /// runtime, since REPLACE of NULL is NULL, but EF's null-semantics pass can only
    /// prune a redundant guard when it recognises the operand as a column it knows
    /// to be NOT NULL - and it does not recognise a nest of REPLACEs as one.</para></summary>
    private static Expression NullGuardTarget(Expression body)
    {
        var target = body;
        while (target is MethodCallExpression { Method.Name: nameof(string.Replace), Object: { } inner })
        {
            target = inner;
        }
        return target;
    }

    private static MemberExpression TermExpression(string term) =>
        Expression.Field(
            Expression.Constant(new GridFilterValue<string> { Value = term }),
            GridFilterValue<string>.ValueField);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        internal static Expression Rebind(
            Expression body, ParameterExpression from, ParameterExpression to) =>
            new ParameterReplacer(from, to).Visit(body)!;

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : node;
    }
}
