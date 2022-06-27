﻿using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Json.More;

// ReSharper disable UnusedAutoPropertyAccessor.Global
#pragma warning disable CS8618

namespace Json.Path.Tests.Suite;

public class ComplianceTestCase
{
	public string Name { get; set; }
	public string Selector { get; set; }
	public JsonNode? Document { get; set; }
	public List<JsonNode?>? Result { get; set; }
	[JsonPropertyName("invalid_selector")]
	public bool InvalidSelector { get; set; }

	public override string ToString()
	{
		var result = Result == null ? null : $"[{string.Join(", ", Result.Select(e => e.AsJsonString()))}]";
		return $"Name:     {Name}\n" +
			   $"Selector: {Selector}\n" +
			   $"Document: {Document}\n" +
			   $"Result:   {result}\n" +
			   $"IsValid:  {!InvalidSelector}";
	}
}