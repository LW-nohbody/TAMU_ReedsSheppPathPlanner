namespace PathPlanningLib.Algorithms.Geometry.Paths;

using PathPlanningLib.Algorithms.Geometry.PathElements;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// Represents a generic path consisting of sequential path elements.
/// This serves as a base class for planner-specific paths (e.g., Reeds-Shepp, Dubins).
/// Telement is the type of path element that composes this path.
public abstract class Path<TElement> : IEnumerable<TElement>
    where TElement : PathElement
{
    // Internal list storing all elements
    protected readonly List<TElement> _elements = new();

    /// Provides read-only access to the elements that make up the path.
    public IReadOnlyList<TElement> Elements => _elements;

    //The total length of the path (computed on demand with ComputeLength()).
    public double Length { get; private set; }

    /// Default constructor that creates an empty path.
    protected Path() { Length = 0; }

    /// Initializes a path from an enumerable sequence of elements.
    protected Path(IEnumerable<TElement> elements)
    {
        if (elements is null)
            throw new ArgumentNullException(nameof(elements));
        _elements.AddRange(elements);
    }

    /// Adds an element to the end of the path.
    public virtual void Add(TElement element)
    {
        if (element is null)
            throw new ArgumentNullException(nameof(element));
        _elements.Add(element);
    }

    /// Removes the first occurrence of a given element from the path.
    public virtual bool Remove(TElement element)
    {
        bool success = _elements.Remove(element);
        return success;
    }

    /// Clears all elements from the path.
    public virtual void Clear()
    {
        _elements.Clear();
        Length = 0.0; 
    }

    /// Returns the total number of elements in the path.
    public int Count => _elements.Count;

    /// Computes the current length of the path
    /// Note: User must compute manually before checking path length
    public abstract void ComputeLength();

    // Returns true if there are no PathElements in the Path
    public bool IsEmpty() => _elements.Count == 0;

    /// Returns an enumerator over the path elements (supports foreach).
    public IEnumerator<TElement> GetEnumerator() => _elements.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// Returns a formatted string describing the path.
    public override string ToString()
    {
        ComputeLength(); // ensure Length is up to date
        return $"{GetType().Name}: {Count} elements, total distance {Math.Round(Length, 3)}";
    }
}
