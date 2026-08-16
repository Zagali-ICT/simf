// Tests: SIMF.Api.Tests/GridColumnsTests.cs, SIMF.Api.Tests/GridColumnConverterTests.cs
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace SIMF.Common.Grids;

/// <summary>
/// The single declaration of what a list resource lets the Control Panel sort,
/// filter and search on, plus its page-size policy. Built once into a
/// <c>private static readonly</c> field per service and sealed on first use, so it
/// is safe to share across concurrent requests: every per-request expression tree
/// is built fresh from the shared, immutable declaration.
///
/// <para>
/// The point of the generic <c>Add</c> is that <c>TProp</c> is a compile-time type
/// at the declaration site. That lets the column close over
/// <c>Queryable.OrderBy&lt;TEntity, TProp&gt;</c> bound to the property's real CLR
/// type. An <c>Expression&lt;Func&lt;T, object&gt;&gt;</c> column table cannot: it
/// forces a <c>Convert</c> node into the tree, which EF Core either refuses to
/// translate or translates into an ordering over the boxed value, so a
/// <c>DisplayOrder</c> of 10 sorts before 2. Here there is no <c>Convert</c>, no
/// reflection and no boxing on the ordering path at all.
/// </para>
///
/// <para>
/// Keys are matched with <see cref="StringComparer.OrdinalIgnoreCase"/>. That is
/// not a convenience: <see cref="GridQuery.Filters"/> is a plain
/// <c>Dictionary&lt;string, string&gt;</c> with the default, case-SENSITIVE
/// comparer, and <c>SimfDataGrid</c> sends the column key verbatim in camelCase
/// (<c>nameArabic</c>, <c>displayOrder</c>) while the hand-written services this
/// replaces matched on <c>ToLowerInvariant()</c> forms. Here is the only place the
/// two can be reconciled.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The entity the list queries.</typeparam>
public sealed class GridColumns<TEntity>
{
    private readonly Dictionary<string, GridColumn<TEntity>> _columns =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Expression<Func<TEntity, string?>>> _searchSelectors = [];
    private readonly List<(GridColumn<TEntity> Column, bool Descending)> _defaultOrder = [];
    private readonly List<(string Key, MemberInfo Member)> _members = [];

    private volatile bool _isSealed;

    /// <summary>The page size used when the request sets none.</summary>
    public int FallbackTop { get; private set; } = 25;

    /// <summary>The page-size cap; a list endpoint's bound against a client that
    /// asks for everything.</summary>
    public int MaxTop { get; private set; } = 200;

    /// <summary>
    /// The (key, property) pairs of the simple column declarations, so a
    /// model-aware test can assert that none of them sits on a value-converted
    /// column. Keys declared through <c>AddFilter</c>, and selectors that are not a
    /// direct member access, are absent: their predicate is hand-written and is
    /// reviewed as code.
    /// </summary>
    public IReadOnlyList<(string Key, MemberInfo Member)> DeclaredMembers => _members;

    /// <summary>
    /// Declares one column. Every declared column is sortable and, unless its CLR
    /// type has no textual form, filterable; <c>searchable</c> is opt-in and only
    /// legal on <c>string</c> columns, because free-text search is a substring
    /// match.
    ///
    /// <para>
    /// The filter operator is inferred from <typeparamref name="TProp"/>, never
    /// declared: string is a case-insensitive contains, bool / Guid / enum /
    /// numeric parse (enums by NAME, case-insensitively), <c>DateTime</c> and
    /// <c>DateOnly</c> take a Saudi calendar day. A value that will not parse is a
    /// 400, never a skipped predicate.
    /// </para>
    ///
    /// <para>
    /// Two things follow the STORED form, not the CLR form, and are worth knowing
    /// before you read a sort as broken. An enum mapped with
    /// <c>HasConversion&lt;string&gt;()</c> sorts alphabetically, not in
    /// declaration order; one mapped with <c>HasConversion&lt;int&gt;()</c> sorts by
    /// ordinal. And SQL Server orders NULLs first ascending, last descending, which
    /// is not configurable per query — a grid sorted ascending on a nullable column
    /// leads with blank rows. Both match the hand-written behaviour being replaced.
    /// </para>
    ///
    /// <para>
    /// A misdeclaration throws here, at declaration time. Callers build the column
    /// set in a static field initializer, so that surfaces as a
    /// <c>TypeInitializationException</c> on the resource's first request and on the
    /// first test that touches it: loud, and before any user sees it.
    /// </para>
    /// </summary>
    /// <typeparam name="TProp">The property's real CLR type. Keep it inferred.</typeparam>
    /// <param name="key">The column key the grid sends, verbatim (camelCase).</param>
    /// <param name="selector">The property selector, e.g. <c>theme =&gt; theme.Code</c>.</param>
    /// <param name="searchable">True to include the column in the free-text search.</param>
    public GridColumns<TEntity> Add<TProp>(
        string key,
        Expression<Func<TEntity, TProp>> selector,
        bool searchable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(selector);
        RequireUnsealed();

        if (searchable && typeof(TProp) != typeof(string))
        {
            throw new ArgumentException(
                $"The grid column '{key}' is declared searchable but is a " +
                $"{typeof(TProp).Name}. Only string columns can be searched.",
                nameof(searchable));
        }

        var column = new GridColumn<TEntity>
        {
            // TProp is bound HERE, at declaration, so both delegates carry the
            // property's real CLR type into the query. Nothing at query time has to
            // discover it, and no Convert node is ever created.
            OrderBy = (rows, descending) =>
                descending ? rows.OrderByDescending(selector) : rows.OrderBy(selector),
            ThenBy = (rows, descending) =>
                descending ? rows.ThenByDescending(selector) : rows.ThenBy(selector),

            Filter = BuildFilter(key, selector),
        };

        AddColumn(key, column);

        if (searchable)
        {
            // TProp is string, so the two closed generic types are identical at
            // runtime: nullable reference annotations are erased.
            _searchSelectors.Add((Expression<Func<TEntity, string?>>)(object)selector);
        }

        if (selector.Body is MemberExpression { Expression: ParameterExpression } member)
        {
            _members.Add((key, member.Member));
        }

        return this;
    }

    /// <summary>
    /// Declares a filter key whose predicate is not a plain comparison on one
    /// property: a half-open date range, a navigation subquery, a comma-separated
    /// list of ids, a sentinel that means "no filter". The key still joins the
    /// closed allow-list, so an unknown key still 400s; only the predicate is
    /// bespoke. The key is filter-only and is not sortable.
    ///
    /// <para>
    /// The builder receives the trimmed raw value and MUST either return a
    /// predicate or throw <see cref="ApiException"/>. The signature has no null
    /// return, because a null predicate is the silently-skipped filter this design
    /// exists to make unexpressible. Use <c>GridFilters.ValueInvalid</c> for the
    /// standard bilingual 400.
    /// </para>
    /// </summary>
    public GridColumns<TEntity> AddFilter(
        string key, Func<string, Expression<Func<TEntity, bool>>> build)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(build);
        RequireUnsealed();

        AddColumn(key, new GridColumn<TEntity> { Filter = build });
        return this;
    }

    /// <summary>
    /// Appends one level to the order used when the request names no sort column.
    /// Call it once per level, in order:
    /// <c>.DefaultOrder("displayOrder").DefaultOrder("name")</c>.
    ///
    /// <para>
    /// The natural order belongs to the resource, not to the request, so it is
    /// declared beside the columns rather than passed on every call. That keeps
    /// "one declaration, one call" intact and lets the key be validated at
    /// declaration time instead of on a user's request. Unset, the natural order is
    /// the tiebreak alone.
    /// </para>
    /// </summary>
    public GridColumns<TEntity> DefaultOrder(string key, bool descending = false)
    {
        RequireUnsealed();

        if (!_columns.TryGetValue(key, out var column) || column.OrderBy is null)
        {
            throw new ArgumentException(
                $"The grid default order names '{key}', which is not a sortable column.",
                nameof(key));
        }

        _defaultOrder.Add((column, descending));
        return this;
    }

    /// <summary>
    /// The resource's page-size policy. Declared beside the columns because it is a
    /// property of the resource, not of the request — which also keeps the
    /// cancellation token last on the call, where it cannot be silently forgotten
    /// behind two optional <c>int</c>s.
    /// </summary>
    public GridColumns<TEntity> PageSize(int fallback = 25, int max = 200)
    {
        RequireUnsealed();
        ArgumentOutOfRangeException.ThrowIfLessThan(fallback, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, fallback);

        FallbackTop = fallback;
        MaxTop = max;
        return this;
    }

    /// <summary>Closes the declaration. Called on the first query; every later
    /// mutation attempt throws rather than racing the readers.</summary>
    internal void Seal() => _isSealed = true;

    internal IReadOnlyList<Expression<Func<TEntity, string?>>> SearchSelectors => _searchSelectors;

    internal IReadOnlyList<(GridColumn<TEntity> Column, bool Descending)> DefaultOrderLevels =>
        _defaultOrder;

    /// <summary>Resolves a requested sort key, or 400s naming it. An unknown sort
    /// key used to fall through a <c>_ =&gt;</c> catch-all: the arrow flipped, the
    /// rows did not move, and the operator concluded the data was wrong rather than
    /// the sort.</summary>
    internal GridColumn<TEntity> RequireSortColumn(string key)
    {
        if (_columns.TryGetValue(key, out var column) && column.OrderBy is not null)
        {
            return column;
        }

        throw new ApiException(
            ErrorCodes.GridSortKeyInvalid, 400,
            $"'{key}' is not a sortable column on this list. Sortable columns: {SortableKeys}.",
            $"لا يمكن الفرز حسب العمود '{key}' في هذه القائمة. الأعمدة المتاحة للفرز: {SortableKeys}.",
            [new ApiErrorDetail
            {
                Field = "sort",
                Message = $"Unknown sort column '{key}'.",
                MessageArabic = $"عمود فرز غير معروف '{key}'.",
            }]);
    }

    /// <summary>Resolves a requested filter key into its predicate builder, or 400s
    /// naming it.</summary>
    internal Func<string, Expression<Func<TEntity, bool>>> RequireFilter(string key)
    {
        if (_columns.TryGetValue(key, out var column) && column.Filter is not null)
        {
            return column.Filter;
        }

        throw new ApiException(
            ErrorCodes.GridFilterKeyInvalid, 400,
            $"'{key}' is not a filterable column on this list. Filterable columns: {FilterableKeys}.",
            $"لا يمكن التصفية حسب العمود '{key}' في هذه القائمة. الأعمدة المتاحة للتصفية: {FilterableKeys}.",
            [new ApiErrorDetail
            {
                Field = key,
                Message = $"Unknown filter column '{key}'.",
                MessageArabic = $"عمود تصفية غير معروف '{key}'.",
            }]);
    }

    private string SortableKeys => string.Join(
        ", ", _columns.Where(entry => entry.Value.OrderBy is not null).Select(entry => entry.Key));

    private string FilterableKeys => string.Join(
        ", ", _columns.Where(entry => entry.Value.Filter is not null).Select(entry => entry.Key));

    private void AddColumn(string key, GridColumn<TEntity> column)
    {
        if (!_columns.TryAdd(key, column))
        {
            throw new ArgumentException(
                $"The grid column '{key}' is declared twice (keys are case-insensitive).",
                nameof(key));
        }
    }

    private void RequireUnsealed()
    {
        if (_isSealed)
        {
            throw new InvalidOperationException(
                "Grid columns are declared once, in a static initializer, and are read " +
                "concurrently by every request afterwards. Mutating the set after the " +
                "first query would race those readers.");
        }
    }

    // ---- filter construction (declaration time; TProp is still statically known) ----

    private static Func<string, Expression<Func<TEntity, bool>>>? BuildFilter<TProp>(
        string key, Expression<Func<TEntity, TProp>> selector)
    {
        if (typeof(TProp) == typeof(string))
        {
            var text = (Expression<Func<TEntity, string?>>)(object)selector;
            return raw => GridSearchPredicate.Contains(text, raw);
        }

        var underlying = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);

        if (underlying == typeof(DateTime))
        {
            // A date filter names a calendar DAY, and the day is half-open
            // [d, d + 1) so a stored 14:30 on that day matches. Saudi wall-clock,
            // per GridFilters.ParseDay.
            return raw => DayRange(selector, GridFilters.ParseDay(key, raw));
        }

        if (underlying == typeof(DateOnly))
        {
            return raw => Equality(selector, Cast<TProp>(GridFilters.ParseDate(key, raw)));
        }

        var parse = ScalarParser(underlying);
        if (parse is null)
        {
            // No textual form for this CLR type. The column stays sortable; a filter
            // on it is a loud 400 rather than a quietly dropped predicate.
            return null;
        }

        return raw =>
        {
            var parsed = parse(raw) ?? throw GridFilters.ValueInvalid(key, raw, Describe(underlying));
            return Equality(selector, Cast<TProp>(parsed));
        };
    }

    private static Expression<Func<TEntity, bool>> Equality<TProp>(
        Expression<Func<TEntity, TProp>> selector, TProp value)
    {
        // The value is parked in a one-field object and read through a member
        // access, which is exactly the shape the C# compiler emits for a captured
        // local, so EF Core's parameter extraction turns it into a SQL parameter.
        // Expression.Constant would instead inline it as a SQL literal, and a grid
        // filter is unbounded user text, which would fill the plan cache with one
        // plan per distinct term.
        var box = new GridFilterValue<TProp> { Value = value };

        return Expression.Lambda<Func<TEntity, bool>>(
            Expression.Equal(
                selector.Body,
                Expression.Field(Expression.Constant(box), GridFilterValue<TProp>.ValueField),
                liftToNull: false,
                method: null),
            selector.Parameters);
    }

    // liftToNull:false keeps the lambda body bool rather than bool?, so on a
    // nullable column EF emits three-valued SQL and NULL rows fall outside the
    // range. That is what the C# semantics say, and it means there is no way to
    // filter FOR null through this design; an "is empty" filter would be a
    // separately declared AddFilter, not a fallout of this one.
    private static Expression<Func<TEntity, bool>> DayRange<TProp>(
        Expression<Func<TEntity, TProp>> selector, DateTime day)
    {
        var from = new GridFilterValue<TProp> { Value = Cast<TProp>(day) };
        var to = new GridFilterValue<TProp> { Value = Cast<TProp>(day.AddDays(1)) };

        var lower = Expression.GreaterThanOrEqual(
            selector.Body,
            Expression.Field(Expression.Constant(from), GridFilterValue<TProp>.ValueField),
            liftToNull: false,
            method: null);

        var upper = Expression.LessThan(
            selector.Body,
            Expression.Field(Expression.Constant(to), GridFilterValue<TProp>.ValueField),
            liftToNull: false,
            method: null);

        return Expression.Lambda<Func<TEntity, bool>>(
            Expression.AndAlso(lower, upper), selector.Parameters);
    }

    // One shared parse table instead of a generic delegate cast per supported type
    // plus a lift for every Nullable<T>. It costs one boxed value per filtered
    // request; that value is unboxed into the typed GridFilterValue<TProp> before
    // any expression node is built, so nothing boxed ever reaches the tree, the
    // ORDER BY, or the SQL.
    private static Func<string, object?>? ScalarParser(Type underlying)
    {
        if (underlying.IsEnum)
        {
            // Enums are a NAME contract on the wire. Enum.TryParse also accepts the
            // numeric form, so the name is checked first: "2" must not quietly
            // become the second member, and "99" must not become a phantom value
            // that matches no persisted row.
            return raw =>
                Array.Exists(
                    Enum.GetNames(underlying),
                    name => string.Equals(name, raw, StringComparison.OrdinalIgnoreCase))
                && Enum.TryParse(underlying, raw, ignoreCase: true, out var value)
                    ? value
                    : null;
        }

        if (underlying == typeof(bool))
        {
            return raw => bool.TryParse(raw, out var value) ? value : null;
        }

        if (underlying == typeof(Guid))
        {
            return raw => Guid.TryParse(raw, out var value) ? value : null;
        }

        if (underlying == typeof(int))
        {
            return raw => int.TryParse(
                raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        if (underlying == typeof(long))
        {
            return raw => long.TryParse(
                raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        if (underlying == typeof(short))
        {
            return raw => short.TryParse(
                raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        if (underlying == typeof(byte))
        {
            return raw => byte.TryParse(
                raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        if (underlying == typeof(decimal))
        {
            return raw => decimal.TryParse(
                raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        if (underlying == typeof(double))
        {
            return raw => double.TryParse(
                raw, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        if (underlying == typeof(TimeOnly))
        {
            return raw => TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, out var value)
                ? value : null;
        }

        return null;
    }

    // Unboxing a boxed T into Nullable<T> is a supported unbox.any conversion, so
    // this one line covers both the nullable and the non-nullable column of every
    // scalar type.
    private static TProp Cast<TProp>(object value) => (TProp)value;

    private static string Describe(Type type) =>
        type.IsEnum
            ? $"one of: {string.Join(", ", Enum.GetNames(type))}"
            : type.Name;
}

/// <summary>One declared column: how to order by it, and how to turn a raw filter
/// string into a predicate over it. Both are delegates built at declaration time
/// and closed over the typed selector.</summary>
internal sealed class GridColumn<TEntity>
{
    /// <summary>Null for a filter-only key declared through <c>AddFilter</c>.</summary>
    internal Func<IQueryable<TEntity>, bool, IOrderedQueryable<TEntity>>? OrderBy { get; init; }

    /// <summary>Null for a filter-only key declared through <c>AddFilter</c>.</summary>
    internal Func<IOrderedQueryable<TEntity>, bool, IOrderedQueryable<TEntity>>? ThenBy { get; init; }

    /// <summary>Null when the column's CLR type has no textual filter form.</summary>
    internal Func<string, Expression<Func<TEntity, bool>>>? Filter { get; init; }
}

/// <summary>
/// The one-field closure a filter value is parked in so EF Core parameterises it.
/// A fresh instance is created per request; the column set is static and shared, so
/// nothing here is ever mutated after construction.
/// </summary>
internal sealed class GridFilterValue<TValue>
{
    internal static readonly FieldInfo ValueField =
        typeof(GridFilterValue<TValue>).GetField(nameof(Value))!;

    public TValue Value = default!;
}
