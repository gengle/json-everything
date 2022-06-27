﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Json.More;

/// <summary>
/// Provides extension functionality for <see cref="JsonNode"/>.
/// </summary>
public static class JsonNodeExtensions
{
	/// <summary>
	/// Determines JSON-compatible equivalence.
	/// </summary>
	/// <param name="a">The first element.</param>
	/// <param name="b">The second element.</param>
	/// <returns>`true` if the element are equivalent; `false` otherwise.</returns>
	public static bool IsEquivalentTo(this JsonNode? a, JsonNode? b)
	{
		switch (a, b)
		{
			case (null, null):
				return true;
			case (JsonObject objA, JsonObject objB):
				if (objA.Count != objB.Count) return false;
				var grouped = objA.Concat(objB)
					.GroupBy(p => p.Key)
					.Select(g => g.ToList())
					.ToList();
				return grouped.All(g => g.Count == 2 && g[0].Value.IsEquivalentTo(g[1].Value));
			case (JsonArray arrayA, JsonArray arrayB):
				if (arrayA.Count != arrayB.Count) return false;
				var zipped = arrayA.Zip(arrayB, (ae, be) => (ae, be));
				return zipped.All(p => p.ae.IsEquivalentTo(p.be));
			case (JsonValue aValue, JsonValue bValue):
				if (aValue.GetValue<object>() is JsonElement aElement &&
					bValue.GetValue<object>() is JsonElement bElement)
					return aElement.IsEquivalentTo(bElement);
				return a.ToJsonString() == b.ToJsonString();
			default:
				return a?.ToJsonString() == b?.ToJsonString();
		}
	}

	// source: https://stackoverflow.com/a/60592310/878701, modified for netstandard2.0
	// license: https://creativecommons.org/licenses/by-sa/4.0/
	/// <summary>
	/// Generate a consistent JSON-value-based hash code for the element.
	/// </summary>
	/// <param name="node">The element.</param>
	/// <param name="maxHashDepth">Maximum depth to calculate.  Default is -1 which utilizes the entire structure without limitation.</param>
	/// <returns>The hash code.</returns>
	/// <remarks>
	/// See the following for discussion on why the default implementation is insufficient:
	///
	/// - https://github.com/gregsdennis/json-everything/issues/76
	/// - https://github.com/dotnet/runtime/issues/33388
	/// </remarks>
	public static int GetEquivalenceHashCode(this JsonNode node, int maxHashDepth = -1)
	{
		static void Add(ref int current, object? newValue)
		{
			unchecked
			{
				current = current * 397 ^ (newValue?.GetHashCode() ?? 0);
			}
		}

		// ReSharper disable once InconsistentNaming
		void ComputeHashCode(JsonNode? target, ref int current, int depth)
		{
			if (target == null) return;

			Add(ref current, target.GetType());

			switch (target)
			{
				case JsonArray array:
					if (depth != maxHashDepth)
						foreach (var item in array)
							ComputeHashCode(item, ref current, depth + 1);
					else
						Add(ref current, array.Count);
					break;

				case JsonObject obj:
					foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
					{
						Add(ref current, property.Key);
						if (depth != maxHashDepth)
							ComputeHashCode(property.Value, ref current, depth + 1);
					}
					break;
				default:
					var value = target.AsValue();
					if (value.TryGetValue<bool>(out var boolA))
						Add(ref current, boolA);
					else
					{
						var number = value.GetNumber();
						if (number != null)
							Add(ref current, number);
						else if (value.TryGetValue<string>(out var stringA))
							Add(ref current, stringA);
					}

					break;
			}
		}

		var hash = 0;
		ComputeHashCode(node, ref hash, 0);
		return hash;
	}

	/// <summary>
	/// Gets JSON string representation for <see cref="JsonNode"/>, including null support.
	/// </summary>
	/// <param name="node">A node.</param>
	/// <returns>JSON string representation.</returns>
	public static string AsJsonString(this JsonNode? node)
	{
		return node?.ToJsonString() ?? "null";
	}

	/// <summary>
	/// Gets a node's underlying numeric value.
	/// </summary>
	/// <param name="value">A JSON value.</param>
	/// <returns>Gets the underlying numeric value, or null if the node represented a non-numeric value.</returns>
	public static decimal? GetNumber(this JsonValue value)
	{
		if (value.TryGetValue(out JsonElement e))
		{
			if (e.ValueKind != JsonValueKind.Number) return null;
			return e.GetDecimal();
		}

		var number = GetTypedInteger(value);
		if (number != null) return number;

		if (value.TryGetValue(out float f)) return (decimal)f;
		if (value.TryGetValue(out double d)) return (decimal)d;
		if (value.TryGetValue(out decimal dc)) return dc;

		return null;
	}

	/// <summary>
	/// Gets a node's underlying numeric value if it's an integer.
	/// </summary>
	/// <param name="value">A JSON value.</param>
	/// <returns>Gets the underlying numeric value if it's an integer, or null if the node represented a non-integer value.</returns>
	public static long? GetInteger(this JsonValue value)
	{
		decimal dc;
		if (value.TryGetValue(out JsonElement e))
		{
			if (e.ValueKind != JsonValueKind.Number) return null;
			dc = e.GetDecimal();
			if (dc == Math.Floor(dc)) return (long)dc;
			return null;
		}

		var number = GetTypedInteger(value);
		if (number != null) return number;

		// ReSharper disable CompareOfFloatsByEqualityOperator
		if (value.TryGetValue(out float f) && f == Math.Floor(f)) return (long)f;
		if (value.TryGetValue(out double d) && d == Math.Floor(d)) return (long)d;
		// ReSharper restore CompareOfFloatsByEqualityOperator
		if (value.TryGetValue(out dc) && dc == Math.Floor(dc)) return (long) dc;

		return null;
	}

	private static long? GetTypedInteger(this JsonValue value)
	{
		if (value.TryGetValue(out byte b)) return b;
		if (value.TryGetValue(out short s)) return s;
		if (value.TryGetValue(out ushort us)) return us;
		if (value.TryGetValue(out int i)) return i;
		if (value.TryGetValue(out ushort ui)) return ui;
		if (value.TryGetValue(out long l)) return l;
		// this doesn't feel right... throw?
		if (value.TryGetValue(out ulong ul)) return (long)ul;

		return null;
	}

	/// <summary>
	/// Gets whether the underlying data is a number.
	/// </summary>
	/// <param name="value">The value.</param>
	/// <returns>`true` if the underlying data is a numeric type or
	/// a <see cref="JsonElement"/> representing a number; `false` otherwise.</returns>
	public static bool IsNumber(this JsonValue value)
	{
		var obj = value.GetValue<object>();
		return obj.GetType().IsNumber() || obj is JsonElement { ValueKind: JsonValueKind.Number };
	}

	/// <summary>
	/// Creates a copy of a node by passing it through the serializer.
	/// </summary>
	/// <param name="source">A node.</param>
	/// <returns>A duplicate of the node.</returns>
	/// <remarks>
	///	`JsonNode` may only be part of a single JSON tree, i.e. have a single parent.
	/// Copying a node allows its value to be saved to another JSON tree.
	/// </remarks>
	public static JsonNode? Copy(this JsonNode? source)
	{
		return source.Deserialize<JsonNode?>();
	}

	/// <summary>
	/// Convenience method that wraps <see cref="JsonObject.TryGetPropertyValue(string, out JsonNode?)"/>
	/// and catches argument exceptions.
	/// </summary>
	/// <param name="obj">The JSON object.</param>
	/// <param name="propertyName">The property name</param>
	/// <param name="node">The node under the property name if it exists and is singular; null otherwise.</param>
	/// <param name="e">An exception if one was thrown during the access attempt.</param>
	/// <returns>true if the property exists and is singular within the JSON data.</returns>
	/// <remarks>
	/// <see cref="JsonObject.TryGetPropertyValue(string, out JsonNode?)"/> throws an
	/// <see cref="ArgumentException"/> if the node was parsed from data that has duplicate
	/// keys.  Please see https://github.com/dotnet/runtime/issues/70604 for more information.
	/// </remarks>
	public static bool TryGetValue(this JsonObject obj, string propertyName, out JsonNode? node, out Exception? e)
	{
		e = null;
		try
		{
			return obj.TryGetPropertyValue(propertyName, out node);
		}
		catch (ArgumentException ae)
		{
			e = ae;
			node = null;
			return false;
		}
	}

	/// <summary>
	/// Creates a new <see cref="JsonArray"/> from an enumerable of nodes.
	/// </summary>
	/// <param name="nodes">The nodes.</param>
	/// <returns>A JSON array.</returns>
	public static JsonArray ToJsonArray(this IEnumerable<JsonNode?> nodes)
	{
		return new JsonArray(nodes.Select(x => x.Copy()).ToArray());
	}
}