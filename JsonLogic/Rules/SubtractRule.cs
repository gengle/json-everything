﻿using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Json.Logic.Rules;

[Operator("-")]
internal class SubtractRule : Rule
{
	private readonly List<Rule> _items;

	public SubtractRule(Rule a, params Rule[] more)
	{
		_items = new List<Rule> { a };
		_items.AddRange(more);
	}

	public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
	{
		if (_items.Count == 0) return 0;

		var value = _items[0].Apply(data, contextData);
		var number = value.Numberify();

		if (number == null)
			throw new JsonLogicException($"Cannot subtract {value.JsonType()}.");

		var result = number.Value;

		if (_items.Count == 1) return -result;

		foreach (var item in _items.Skip(1))
		{
			value = item.Apply(data, contextData);

			number = value.Numberify();

			if (number == null)
				throw new JsonLogicException($"Cannot subtract {value.JsonType()}.");

			result -= number.Value;
		}

		return result;
	}
}