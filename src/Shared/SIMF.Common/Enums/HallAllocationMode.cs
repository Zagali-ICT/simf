namespace SIMF.Common.Enums;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — how space inside a <c>Hall</c> is reserved
/// for a purpose over a from–to time-slot. The owner's words: reservation can be
/// "whole, rand by count, by row column pre-reserved".
/// </summary>
public enum HallAllocationMode
{
    /// <summary>Reserve the entire hall for the slot.</summary>
    Whole = 0,

    /// <summary>Allocate a number of units (tables/seats) at random, stopping at
    /// the requested count or the hall capacity, whichever is smaller.</summary>
    RandomByCount = 1,

    /// <summary>Reserve specific, pre-named rows/columns (a CSV spec).</summary>
    RowColumn = 2,
}
