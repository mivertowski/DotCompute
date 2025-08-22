// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections;
using System.Linq.Expressions;

namespace DotCompute.Linq.Analysis;

/// <summary>
/// Visitor for estimating data size in expressions.
/// </summary>
/// <remarks>
/// This visitor analyzes expression trees to estimate the size of data involved
/// in queries, which helps in making decisions about GPU vs CPU execution
/// and memory allocation strategies.
/// </remarks>
internal class DataSizeEstimator : ExpressionVisitor
{
    /// <summary>
    /// Gets the estimated size of data in the expression.
    /// </summary>
    /// <value>
    /// The estimated number of elements or data size, used for execution planning.
    /// </value>
    public long EstimatedSize { get; private set; }

    /// <summary>
    /// Visits constant expressions to estimate data sizes.
    /// </summary>
    /// <param name="node">The constant expression to analyze.</param>
    /// <returns>The potentially modified expression.</returns>
    /// <remarks>
    /// This method examines constant values to determine data sizes for arrays,
    /// collections, and queryables, providing estimates for memory planning.
    /// </remarks>
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is Array array)
        {
            EstimatedSize = Math.Max(EstimatedSize, array.Length);
        }
        else if (node.Value is ICollection collection)
        {
            EstimatedSize = Math.Max(EstimatedSize, collection.Count);
        }
        else if (node.Value is IQueryable queryable)
        {
            try
            {
                // Try to get count if possible
                var count = queryable.Cast<object>().Count();
                EstimatedSize = Math.Max(EstimatedSize, count);
            }
            catch
            {
                // Fallback to default size
                EstimatedSize = Math.Max(EstimatedSize, 1000);
            }
        }

        return base.VisitConstant(node);
    }
}