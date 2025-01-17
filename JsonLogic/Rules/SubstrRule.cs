﻿using System;
using System.Text.Json.Nodes;
using Json.More;

namespace Json.Logic.Rules;

[Operator("substr")]
internal class SubstrRule : Rule
{
	private readonly Rule _input;
	private readonly Rule _start;
	private readonly Rule? _count;

	public SubstrRule(Rule input, Rule start)
	{
		_input = input;
		_start = start;
	}
	public SubstrRule(Rule input, Rule start, Rule count)
	{
		_input = input;
		_start = start;
		_count = count;
	}

	public override JsonNode? Apply(JsonNode? data, JsonNode? contextData = null)
	{
		var input = _input.Apply(data, contextData);
		var start = _start.Apply(data, contextData);

		if (input is not JsonValue inputValue || !inputValue.TryGetValue(out string? stringInput))
			throw new JsonLogicException($"Cannot substring a {input.JsonType()}.");

		if (start is not JsonValue startValue || startValue.GetInteger() == null)
			throw new JsonLogicException("Start value must be an integer");

		var numberStart = (int)startValue.GetInteger()!.Value;

		if (numberStart < -stringInput.Length) return input;
		if (numberStart < 0)
			numberStart = Math.Max(stringInput.Length + numberStart, 0);
		if (numberStart >= stringInput.Length) return string.Empty;

		if (_count == null) return stringInput[numberStart..];

		var count = _count.Apply(data, contextData);
		if (count is not JsonValue countValue || countValue.GetInteger() == null)
			throw new JsonLogicException("Count value must be an integer");

		var integerCount = (int)countValue.GetInteger()!.Value;
		var availableLength = stringInput.Length - numberStart;
		if (integerCount < 0)
			integerCount = Math.Max(availableLength + integerCount, 0);
		integerCount = Math.Min(availableLength, integerCount);

		return stringInput.Substring(numberStart, integerCount);
	}
}