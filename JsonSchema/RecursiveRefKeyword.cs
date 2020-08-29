﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Pointer;

namespace Json.Schema
{
	/// <summary>
	/// Handles `$recursiveRef`.
	/// </summary>
	[SchemaKeyword(Name)]
	[SchemaDraft(Draft.Draft201909)]
	[Vocabulary(Vocabularies.Core201909Id)]
	[JsonConverter(typeof(RecursiveRefKeywordJsonConverter))]
	public class RecursiveRefKeyword : IJsonSchemaKeyword
	{
		internal const string Name = "$recursiveRef";

		/// <summary>
		/// The URI reference.
		/// </summary>
		public Uri Reference { get; }

		/// <summary>
		/// Creates a new <see cref="RecursiveRefKeyword"/>.
		/// </summary>
		/// <param name="value"></param>
		public RecursiveRefKeyword(Uri value)
		{
			Reference = value;
		}

		/// <summary>
		/// Provides validation for the keyword.
		/// </summary>
		/// <param name="context">Contextual details for the validation process.</param>
		public void Validate(ValidationContext context)
		{
			var parts = Reference.OriginalString.Split(new []{'#'}, StringSplitOptions.None);
			var baseUri = parts[0];
			var fragment = parts.Length > 1 ? parts[1] : null;

			Uri newUri;
			JsonSchema baseSchema = null;
			if (!string.IsNullOrEmpty(baseUri))
			{
				if (Uri.TryCreate(baseUri, UriKind.Absolute, out newUri))
					baseSchema = context.Options.SchemaRegistry.Get(newUri);
				else if (context.CurrentUri != null)
				{
					var uriFolder = context.CurrentUri.OriginalString.EndsWith("/")
						? context.CurrentUri
						: context.CurrentUri.GetParentUri();
					newUri = uriFolder;
					var newBaseUri = new Uri(uriFolder, baseUri);
					if (!string.IsNullOrEmpty(fragment))
						newUri = newBaseUri;
					baseSchema = context.Options.SchemaRegistry.Get(newBaseUri);
				}
			}
			else
			{
				baseSchema = context.CurrentAnchor ?? context.SchemaRoot;
				newUri = context.CurrentUri;
			}

			JsonSchema schema;
			if (!string.IsNullOrEmpty(fragment) && AnchorKeyword.AnchorPattern.IsMatch(fragment))
				schema = context.Options.SchemaRegistry.Get(newUri, fragment);
			else
			{
				if (baseSchema == null)
				{
					context.IsValid = false;
					context.Message = $"Could not resolve base URI `{baseUri}`";
					return;
				}

				if (!string.IsNullOrEmpty(fragment))
				{
					fragment = $"#{fragment}";
					if (!JsonPointer.TryParse(fragment, out var pointer))
					{
						context.IsValid = false;
						context.Message = $"Could not parse pointer `{fragment}`";
						return;
					}
					(schema, newUri) = baseSchema.FindSubschema(pointer, newUri);
				}
				else
					schema = baseSchema;
			}

			if (schema == null)
			{
				context.IsValid = false;
				context.Message = $"Could not resolve RecursiveReference `{Reference}`";
				return;
			}

			var subContext = ValidationContext.From(context, newUri: newUri);
			if (!ReferenceEquals(baseSchema, context.SchemaRoot)) 
				subContext.SchemaRoot = baseSchema;
			schema.ValidateSubschema(subContext);
			context.NestedContexts.Add(subContext);
			context.ConsolidateAnnotations();
			context.IsValid = subContext.IsValid;
		}
	}

	internal class RecursiveRefKeywordJsonConverter : JsonConverter<RecursiveRefKeyword>
	{
		public override RecursiveRefKeyword Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var uri = reader.GetString(); 
			return new RecursiveRefKeyword(new Uri(uri, UriKind.RelativeOrAbsolute));


		}
		public override void Write(Utf8JsonWriter writer, RecursiveRefKeyword value, JsonSerializerOptions options)
		{
			writer.WritePropertyName(RecursiveRefKeyword.Name);
			JsonSerializer.Serialize(writer, value.Reference, options);
		}
	}

	// Source: https://github.com/WebDAVSharp/WebDAVSharp.Server/blob/1d2086a502937936ebc6bfe19cfa15d855be1c31/WebDAVExtensions.cs
}