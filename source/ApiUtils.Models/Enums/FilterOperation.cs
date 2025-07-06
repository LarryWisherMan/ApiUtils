namespace ApiUtils.Models;

/// <summary>
/// Specifies filter operations for REST queries.
/// </summary>
public enum FilterOperation
{
    /// <summary>Equals.</summary>
    Eq,

    /// <summary>Not equals.</summary>
    Ne,

    /// <summary>Greater than.</summary>
    Gt,

    /// <summary>Greater than or equal.</summary>
    Gte,

    /// <summary>Less than.</summary>
    Lt,

    /// <summary>Less than or equal.</summary>
    Lte,

    /// <summary>LIKE operator for pattern matching.</summary>
    Like,

    /// <summary>IN operator for list-based filtering.</summary>
    In,

    /// <summary>BETWEEN operator for range filtering.</summary>
    Between
}
